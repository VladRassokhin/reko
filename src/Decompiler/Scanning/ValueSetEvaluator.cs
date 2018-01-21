#region License
/* 
 * Copyright (C) 1999-2018 John Källén.
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

using Reko.Core.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reko.Core;
using Reko.Core.Operators;
using Reko.Core.Types;

namespace Reko.Scanning
{
    public class ValueSetEvaluator : ExpressionVisitor<ValueSet>
    {
        private Program program;
        private Dictionary<Expression, ValueSet> context;
        private ExpressionValueComparer cmp;

        public ValueSetEvaluator(Program program, Dictionary<Expression, ValueSet> context)
        {
            this.program = program;
            this.context = context;
            this.cmp = new ExpressionValueComparer();
        }

        public ValueSet VisitAddress(Address addr)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitApplication(Application appl)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitArrayAccess(ArrayAccess acc)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitBinaryExpression(BinaryExpression binExp)
        {
            var cLeft = binExp.Left as Constant;
            var cRight = binExp.Right as Constant;
            if (cLeft != null && cRight != null)
            {
                return new IntervalValueSet(
                    cLeft.DataType,
                    StridedInterval.Constant(
                        binExp.Operator.ApplyConstants(cLeft, cRight)));
            }
            if (cLeft == null && cRight != null)
            {
                var left = binExp.Left.Accept(this);
                if (binExp.Operator == Operator.IAdd)
                {
                    return left.Add(cRight);
                }
                else if (binExp.Operator == Operator.And)
                {
                    return left.And(cRight);
                }
                else if (binExp.Operator == Operator.Shl)
                {
                    return left.Shl(cRight);
                }
                else if (binExp.Operator == Operator.IMul)
                {
                    return left.IMul(cRight);
                }
            }
            if (cRight == null && cLeft != null)
            {
                var right = binExp.Right.Accept(this);
                if (binExp.Operator == Operator.IAdd)
                {
                    return right.Add(cLeft);
                }
                else if (binExp.Operator == Operator.And)
                {
                    return right.And(cLeft);
                }
            }
            if (binExp.Operator == Operator.IAdd)
            {
                if (cmp.Equals(binExp.Left, binExp.Right))
                {
                    var left = binExp.Left.Accept(this);
                    return left.Shl(Constant.Int32(1));
                }
            }
            throw new NotImplementedException();
        }

        public ValueSet VisitCast(Cast cast)
        {
            var vs = cast.Expression.Accept(this);
            if (cast.DataType.BitSize < cast.Expression.DataType.BitSize)
            {
                return vs.Truncate(cast.DataType);
            }
            var pt = cast.DataType as PrimitiveType;
            if (pt != null && pt.Domain == Domain.SignedInt)
            {
                return vs.SignExtend(cast.DataType);
            }
            throw new NotImplementedException();
        }

        public ValueSet VisitConditionalExpression(ConditionalExpression cond)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitConditionOf(ConditionOf cof)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitConstant(Constant c)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitDepositBits(DepositBits d)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitDereference(Dereference deref)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitFieldAccess(FieldAccess acc)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitIdentifier(Identifier id)
        {
            ValueSet vs;
            if (context.TryGetValue(id, out vs))
                return vs;
            return new IntervalValueSet(id.DataType, StridedInterval.Empty);
        }

        public ValueSet VisitMemberPointerSelector(MemberPointerSelector mps)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitMemoryAccess(MemoryAccess access)
        {
            var vs = access.EffectiveAddress.Accept(this);
            return new ConcreteValueSet(
                vs.DataType,
                vs.Values
                    .Select(v => ReadValue(access.DataType, v))
                    .ToArray());
        }

        private Constant ReadValue(DataType dt, Constant cAddr)
        {
            var addr = program.SegmentMap.MapLinearAddressToAddress(cAddr.ToUInt64());
            ImageSegment seg;
            if (!program.SegmentMap.TryFindSegment(addr, out seg))
                return Constant.Invalid;
            var rdr = program.Architecture.CreateImageReader(seg.MemoryArea, addr);
            return rdr.Read((PrimitiveType)dt);
        }

        public ValueSet VisitMkSequence(MkSequence seq)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitOutArgument(OutArgument outArgument)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitPhiFunction(PhiFunction phi)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitPointerAddition(PointerAddition pa)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitProcedureConstant(ProcedureConstant pc)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitScopeResolution(ScopeResolution scopeResolution)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitSegmentedAccess(SegmentedAccess access)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitSlice(Slice slice)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitTestCondition(TestCondition tc)
        {
            throw new NotImplementedException();
        }

        public ValueSet VisitUnaryExpression(UnaryExpression unary)
        {
            throw new NotImplementedException();
        }
    }
}
