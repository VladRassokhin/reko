#region License
/* 
 * Copyright (C) 1999-2017 John Källén.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; see the file COPYING.  If not, write to
 * the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 */
#endregion

using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Rtl;
using System.Collections.Generic;
using System;
using Reko.Core.Types;
using Reko.Scanning;
using System.Linq;
using System.Diagnostics;
using Reko.Core.Operators;
using Reko.Core.Lib;

namespace Reko.Scanning
{
    public class BackwardSlicer
    {
        internal static TraceSwitch trace = new TraceSwitch("BackwardSlicer", "Traces the backward slicer") { Level = TraceLevel.Verbose };

        private IBackWalkHost<RtlBlock, RtlInstruction> host;
        private SliceState state;


        public BackwardSlicer(IBackWalkHost<RtlBlock, RtlInstruction> host)
        {
            this.host = host;
            this.Roots = new Dictionary<Expression, BitRange>();
        }

        public Dictionary<Expression, BitRange> Live { get { return state.Live; } }
        public Dictionary<Expression, BitRange> Roots { get; private set; }
        public Expression JumpTableFormat { get { return state.JumpTableFormat; } }  // an expression that computes the destination addresses.

        public Expression JumpTableIndex { get { return state.JumpTableIndex; } }    // an expression that tests the index 
        public StridedInterval JumpTableIndexInterval { get { return state.JumpTableIndexInterval; } }    // an expression that tests the index 

    

        public bool Start(RtlBlock block)
        {
            this.Roots.Clear();
            this.state = new SliceState(this, block);

            DebugEx.PrintIf(trace.TraceInfo, "Bwslc: Starting at instruction {0}", state.instrs[state.iInstr]);
            var sr = state.instrs[state.iInstr].Accept(state);
            state.Live = sr.LiveExprs;
            if (sr.LiveExprs.Count == 0)
            {
                DebugEx.PrintIf(trace.TraceWarning, "  No indirect registers?");
                return false;
            }
            this.Roots = new Dictionary<Expression, BitRange>(sr.LiveExprs);
            DebugEx.PrintIf(trace.TraceVerbose, "  live: {0}", DumpLive(state.Live));
            return true;
        }


        public bool Step()
        {
            --state.iInstr;
            while (state.iInstr < 0)
            {
                DebugEx.PrintIf(trace.TraceVerbose, "Reached beginning of block {0}", state.block.Address);
                var pred = host.GetSinglePredecessor(state.block);    //$TODO: do all predecessors, add to queue.
                if (pred == null)
                {
                    DebugEx.PrintIf(trace.TraceVerbose, "  No predecessors found, stopping");
                    return false;
                }
                this.state.addrSucc = state.block.Address;
                state.block = pred;
                state.instrs = SliceState.FlattenInstructions(state.block);
                state.iInstr = state.instrs.Count - 1;
            }
            DebugEx.PrintIf(trace.TraceInfo, "Bwslc: Stepping to instruction {0}", state.instrs[state.iInstr]);
            var sr = state.instrs[state.iInstr].Accept(state);
            if (sr == null)
            {
                // Instruction had no effect on live registers.
                return true;
            }
            foreach (var de in sr.LiveExprs)
            {
                state.Live[de.Key] = de.Value;
            }
            if (sr.Stop)
            {
                DebugEx.PrintIf(trace.TraceVerbose, "  Was asked to stop, stopping.");
                return false;
            }
            if (state.Live.Count == 0)
            {
                DebugEx.PrintIf(trace.TraceVerbose, "  No more live expressions, stopping.");
                return false;
            }
            DebugEx.PrintIf(trace.TraceVerbose, "  live: {0}", DumpLive(state.Live));
            return true;
        }

        private string DumpLive(Dictionary<Expression, BitRange> live)
        {
            return string.Format("{{ {0} }}",
                string.Join(
                    ",",
                    live
                        .OrderBy(l => l.Key.ToString())
                        .Select(l => $"{{ {l.Key}, {l.Value} }}")));
        }
    }

    public class SliceState : RtlInstructionVisitor<SlicerResult>, ExpressionVisitor<SlicerResult, BitRange>
    {
        private BackwardSlicer slicer;
        public RtlBlock block;
        public int iInstr;
        public List<RtlInstruction> instrs;
        public Address addrSucc;    // the block from which we traced.
        public ConditionCode ccNext; // The condition code that is used in a branch.
        public Expression assignLhs; // current LHS
        public bool invertCondition;
        public Dictionary<Expression, BitRange> Live;
        private ExpressionValueComparer cmp;

        public SliceState(BackwardSlicer slicer, RtlBlock block)
        {
            this.slicer = slicer;
            this.cmp = new ExpressionValueComparer();
            var instrs = FlattenInstructions(block);
            var iLast = instrs.Count - 1;
            this.block = block;
            this.instrs = instrs;
            this.iInstr = iLast;
        }

        public Expression JumpTableFormat { get; private set; }  // an expression that computes the destination addresses.

        public Expression JumpTableIndex { get; private set; }    // an expression that tests the index 
        public StridedInterval JumpTableIndexInterval { get; private set; }    // an expression that tests the index 

        public static List<RtlInstruction> FlattenInstructions(RtlBlock b)
        {
            return b.Instructions.SelectMany(rtlc => rtlc.Instructions).ToList();
        }

        private StorageDomain DomainOf(Expression e)
        {
            var id = e as Identifier;
            if (id != null)
            {
                return id.Storage.Domain;
            }
            throw new NotImplementedException();
        }

        private StridedInterval MakeInterval_ISub(Expression left, Constant right)
        {
            if (right == null)
                return StridedInterval.Empty;
            var cc = this.ccNext;
            if (this.invertCondition)
                cc = cc.Invert();
            switch (cc)
            {
            case ConditionCode.ULE: return StridedInterval.Create(1, 0, right.ToInt64());
            default: throw new NotImplementedException($"Unimplemented condition code {cc}.");
            }
        }

        private StridedInterval MakeInterval_And(Expression left, Constant right)
        {
            if (right == null)
                return StridedInterval.Empty;
            long n = right.ToInt64();
            if (Bits.IsEvenPowerOfTwo(n + 1))
            {
                return StridedInterval.Create(1, 0, n);
            }
            else
            {
                return StridedInterval.Empty;
            }
        }

        public SlicerResult VisitAddress(Address addr, BitRange ctx)
        {
            return new SlicerResult
            {
                SrcExpr = addr,
            };
        }

        public SlicerResult VisitApplication(Application appl, BitRange ctx)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitArrayAccess(ArrayAccess acc, BitRange ctx)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitAssignment(RtlAssignment ass)
        {
            var id = ass.Dst as Identifier;
            if (id == null)
            {
                // Ignore writes to memory.
                return null;
            }
            bool wasLive = false;
            if (id != null)
            {
                wasLive = Live.Remove(id);
                if (!wasLive)
                {
                    // This assignment doesn't affect the end result.
                    return null;
                }
                //$TODO: create edges in graph. storages....
            }
            this.assignLhs = ass.Dst;
            var se = ass.Src.Accept(
                this,
                new BitRange(
                    (short)id.Storage.BitAddress,
                    (short)(id.Storage.BitAddress + id.Storage.BitSize)));
            this.JumpTableFormat = ExpressionReplacer.Replace(ass.Dst, se.SrcExpr, JumpTableFormat);
            this.assignLhs = null;
            return se;
        }

        public SlicerResult VisitBinaryExpression(BinaryExpression binExp, BitRange ctx)
        {
            if (binExp.Operator == Operator.Xor)
            {
                if (cmp.Equals(binExp.Left, binExp.Right))
                {
                    var regDst = assignLhs as Identifier;
                    var regHi = binExp.Left as Identifier;
                    if (regDst != null && regHi != null &&
                        regDst.Storage.OffsetOf(regHi.Storage) == 8)
                    {
                        // The 8086 didn't have a MOVZX instruction, so clearing the high byte of a
                        // register BX was done by issuing XOR BH,BH
                        var seXor = new SlicerResult
                        {
                            SrcExpr = new Cast(regDst.DataType, new Cast(PrimitiveType.Byte, this.assignLhs)),
                            LiveExprs = new Dictionary<Expression, BitRange> { { this.assignLhs, new BitRange(0, 8) } }
                        };
                        return seXor;
                    }
                }
            }
            var seLeft = binExp.Left.Accept(this, ctx);
            var seRight = binExp.Right.Accept(this, ctx);
            if (binExp.Operator == Operator.And)
            {
                this.JumpTableIndexInterval = MakeInterval_And(binExp.Left, binExp.Right as Constant);
                return new SlicerResult
                {
                    SrcExpr = binExp,
                };
            }
            var se = new SlicerResult
            {
                LiveExprs = seLeft.LiveExprs.Concat(seRight.LiveExprs)
                    .GroupBy(e => e.Key)
                    .ToDictionary(k => k.Key, v => v.Max(vv => vv.Value)),
                SrcExpr = binExp,
            };
            return se;
        }

        public SlicerResult VisitBranch(RtlBranch branch)
        {
            var se = branch.Condition.Accept(this, new BitRange(0, 0));
            var addrTarget = branch.Target as Address;
            if (addrTarget == null)
                throw new NotImplementedException();    //#REVIEW: do we ever see this?
            if (this.addrSucc != addrTarget)
            {
                this.invertCondition = true;
            }
            return se;
        }

        public SlicerResult VisitCall(RtlCall call)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitCast(Cast cast, BitRange ctx)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitConditionalExpression(ConditionalExpression c, BitRange context)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitConditionOf(ConditionOf cof, BitRange ctx)
        {
            var bin = cof.Expression as BinaryExpression;
            if (bin != null)
            {
                if (bin.Operator == Operator.ISub)
                {
                    var domLeft = DomainOf(bin.Left);
                    foreach (var live in Live.Keys)
                    {
                        if (DomainOf(live) == domLeft)
                        {
                            if (cmp.Equals(this.assignLhs, this.JumpTableIndex))
                            {
                                this.JumpTableIndexInterval = MakeInterval_ISub(bin.Left, bin.Right as Constant);
                                DebugEx.PrintIf(BackwardSlicer.trace.TraceVerbose, "  Found range of {0}: {1}", live, JumpTableIndexInterval);
                                return new SlicerResult
                                {
                                    SrcExpr = cof,
                                    Stop = true,
                                };
                            }
                        }
                    }
                }
                else
                    throw new NotImplementedException();
            }
            var se = cof.Expression.Accept(this, RangeOf(cof.Expression.DataType));
            se.SrcExpr = cof;
            this.JumpTableIndex = cof.Expression;
            return se;
        }

        public SlicerResult VisitConstant(Constant c, BitRange ctx)
        {
            return new SlicerResult
            {
                LiveExprs = new Dictionary<Expression, BitRange>(),
                SrcExpr = c,
            };
        }

        public SlicerResult VisitDepositBits(DepositBits d, BitRange ctx)
        {
            throw new NotImplementedException();
        }


        public SlicerResult VisitDereference(Dereference deref, BitRange ctx)
        {
            throw new NotImplementedException();
        }


        public SlicerResult VisitFieldAccess(FieldAccess acc, BitRange ctx)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitGoto(RtlGoto go)
        {
            var sr = go.Target.Accept(this, RangeOf(go.Target.DataType));
            if (JumpTableFormat == null)
            {
                JumpTableFormat = go.Target;
            }
            return sr;
        }

        private BitRange RangeOf(DataType dt)
        {
            return new BitRange(0, (short)dt.BitSize);
        }

        public SlicerResult VisitIdentifier(Identifier id, BitRange ctx)
        {
            var sr = new SlicerResult
            {
                LiveExprs = { { id, ctx } },
                SrcExpr = id,
            };
            return sr;
        }

        public SlicerResult VisitIf(RtlIf rtlIf)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitInvalid(RtlInvalid invalid)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitMemberPointerSelector(MemberPointerSelector mps, BitRange ctx)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitMemoryAccess(MemoryAccess access, BitRange ctx)
        {
            var sr = access.EffectiveAddress.Accept(this, ctx);
            return sr;
        }

        public SlicerResult VisitMkSequence(MkSequence seq, BitRange ctx)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitNop(RtlNop rtlNop)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitOutArgument(OutArgument outArgument, BitRange ctx)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitPhiFunction(PhiFunction phi, BitRange ctx)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitPointerAddition(PointerAddition pa, BitRange ctx)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitProcedureConstant(ProcedureConstant pc, BitRange ctx)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitReturn(RtlReturn ret)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitScopeResolution(ScopeResolution scopeResolution, BitRange ctx)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitSegmentedAccess(SegmentedAccess access, BitRange ctx)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitSideEffect(RtlSideEffect side)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitSlice(Slice slice, BitRange ctx)
        {
            throw new NotImplementedException();
        }

        public SlicerResult VisitTestCondition(TestCondition tc, BitRange ctx)
        {
            var se = tc.Expression.Accept(this, RangeOf(tc.Expression.DataType));
            this.ccNext = tc.ConditionCode;
            this.JumpTableIndex = tc.Expression;
            return se;
        }

        public SlicerResult VisitUnaryExpression(UnaryExpression unary, BitRange ctx)
        {
            throw new NotImplementedException();
        }
    }

    public struct BitRange : IComparable<BitRange>
    {
        public readonly short begin;
        public readonly short end;

        public BitRange(short begin, short end)
        {
            this.begin = begin;
            this.end = end;
        }

        public int CompareTo(BitRange that)
        {
            return (this.end - this.end) - (that.end - that.begin);
        }

        public override bool Equals(object obj)
        {
            if (obj != null && obj is BitRange)
            {
                var that = (BitRange)obj;
                return this.begin == that.begin && this.end == that.end;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return begin.GetHashCode() ^ end.GetHashCode() * 5;
        }

        public static bool operator == (BitRange a, BitRange b)
        {
            return a.begin == b.begin && a.end == b.end;
        }

        public static bool operator !=(BitRange a, BitRange b)
        {
            return a.begin != b.begin || a.end != b.end;
                
        }

        public override string ToString()
        {
            return $"[{begin}-{end})";
        }
    }

    public class SlicerResult
    {
        // Live storages are involved in the computation of the jump destinations.
        public Dictionary<Expression, BitRange> LiveExprs = new Dictionary<Expression, BitRange>();
        public Expression SrcExpr;

        public bool Stop { get; internal set; }
    }
}