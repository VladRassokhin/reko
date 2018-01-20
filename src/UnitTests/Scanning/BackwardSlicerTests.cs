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

using NUnit.Framework;
using Reko.Core;
using Reko.Core.Rtl;
using Reko.Scanning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reko.UnitTests.Mocks;
using Reko.Core.Expressions;
using Reko.Core.Lib;

namespace Reko.UnitTests.Scanning
{
    [TestFixture]
    public class BackwardSlicerTests
    {
        private StorageBinder binder;
        private FakeArchitecture arch;
        private RtlBackwalkHost host;
        private Program program;
        private DirectedGraph<RtlBlock> graph;

        [SetUp]
        public void Setup()
        {
            arch = new FakeArchitecture();
            program = new Program {
                Architecture = arch,
                SegmentMap = new SegmentMap(
                    Address.Ptr32(0x00120000),
                    new ImageSegment(
                        ".text",
                        new MemoryArea(Address.Ptr32(0x00120000), new byte[0x10000]),
                        AccessMode.ReadExecute))
            };
            binder = new StorageBinder();
            graph = new DiGraph<RtlBlock>();
            host = new RtlBackwalkHost(program, graph);
        }

        private Identifier Reg(int rn)
        {
            var reg = arch.GetRegister(rn);
            return binder.EnsureRegister(reg);
        }

        private Identifier Cc(string name)
        {
            var cc = arch.GetFlagGroup(name);
            return binder.EnsureFlagGroup(cc);
        }

        private RtlBlock Given_Block(uint uAddr)
        {
            var b = new RtlBlock(Address.Ptr32(uAddr), $"l{uAddr:X8}");
            return b;
        }

        private void Given_Instrs(RtlBlock block, Action<RtlEmitter> b)
        {
            var instrs = new List<RtlInstruction>();
            var trace = new RtlEmitter(instrs);
            b(trace);
            block.Instructions.Add(
                new RtlInstructionCluster(
                    block.Address  + block.Instructions.Count * 4,
                    4,
                    instrs.ToArray()));
        }

        [Test]
        public void Bwslc_DetectRegister()
        {
            var r1 = Reg(1);
            var b = Given_Block(0x10);
            Given_Instrs(b, m => m.Goto(r1));

            var bwslc = new BackwardSlicer(b, host);
            var sr =  b.Instructions.Last().Instructions.Last().Accept(bwslc);

            Assert.AreEqual(sr.LiveExprs[r1], new BitRange(0, 32));
        }

        [Test]
        public void Bwslc_DetectNoRegister()
        {
            var r1 = Reg(1);
            var b = Given_Block(0x10);
            Given_Instrs(b, m => m.Goto(Address.Ptr32(0x00123400)));

            var bwslc = new BackwardSlicer(b, host);
            var sr = b.Instructions.Last().Instructions.Last().Accept(bwslc);

            Assert.AreEqual(0, sr.LiveExprs.Count);
        }

        [Test]
        public void Bwslc_SeedSlicer()
        {
            var r1 = Reg(1);
            var b = Given_Block(0x10);
            Given_Instrs(b, m => m.Goto(r1));

            var bwslc = new BackwardSlicer(b, host);
            var result = bwslc.Start();
            Assert.IsTrue(result);
        }

        [Test]
        public void Bwslc_DetectAddition()
        {
            var r1 = Reg(1);
            var b = Given_Block(0x100);
            Given_Instrs(b, m => m.Goto(m.IAdd(r1, 0x00123400)));

            var bwslc = new BackwardSlicer(b, host);
            var start = bwslc.Start();
            Assert.IsTrue(start);
            Assert.AreEqual(1, bwslc.Roots.Count);
        }

        [Test]
        public void Bwslc_KillLiveness()
        {
            var r1 = Reg(1);
            var r2 = Reg(2);
            var b = Given_Block(0x100);
            Given_Instrs(b, m => { m.Assign(r1, m.Shl(r2, 2)); });
            Given_Instrs(b, m => { m.Goto(m.IAdd(r1, 0x00123400)); });

            var bwslc = new BackwardSlicer(b, host);
            var start = bwslc.Start();
            var step = bwslc.Step();
            Assert.IsTrue(start);
            Assert.IsTrue(step);
            Assert.AreEqual(1, bwslc.Live.Count);
            Assert.AreEqual("r2", bwslc.Live.First().Key.ToString());
        }

        [Test(Description = "Trace across a jump")]
        public void Bwslc_AcrossJump()
        {
            var r1 = Reg(1);
            var r2 = Reg(2);

            var b = Given_Block(0x100);
            Given_Instrs(b, m => { m.Assign(r1, m.Shl(r2, 2)); });
            Given_Instrs(b, m => { m.Goto(Address.Ptr32(0x200)); });

            var b2 = Given_Block(0x200);
            Given_Instrs(b2, m => { m.Goto(m.IAdd(r1, 0x00123400)); });

            graph.Nodes.Add(b);
            graph.Nodes.Add(b2);
            graph.AddEdge(b, b2);

            var bwslc = new BackwardSlicer(b2, host);
            var start = bwslc.Start();  // indirect jump
            bwslc.Step();    // direct jump
            var step = bwslc.Step();    // shift left
            Assert.IsTrue(start); 
            Assert.IsTrue(step); 
            Assert.AreEqual(1, bwslc.Live.Count);
            Assert.AreEqual("r2", bwslc.Live.First().Key.ToString());
        }

        [Test(Description = "Trace across a branch where the branch was taken.")]
        public void Bwslc_BranchTaken()
        {
            var r1 = Reg(1);
            var r2 = Reg(2);
            var cz = Cc("CZ");

            var b = Given_Block(0x100);
            Given_Instrs(b, m => { m.Branch(m.Test(ConditionCode.ULE, cz), Address.Ptr32(0x200), RtlClass.ConditionalTransfer); });

            var b2 = Given_Block(0x200);
            Given_Instrs(b2, m => { m.Assign(r1, m.Shl(r2, 2)); });
            Given_Instrs(b2, m => { m.Goto(m.IAdd(r1, 0x00123400)); });

            graph.Nodes.Add(b);
            graph.Nodes.Add(b2);
            graph.AddEdge(b, b2);

            var bwslc = new BackwardSlicer(b2, host);
            var start = bwslc.Start();  // indirect jump
            bwslc.Step();    // shift left
            var step = bwslc.Step();    // branch

            Assert.IsTrue(start);
            Assert.IsTrue(step);
            Assert.AreEqual(2, bwslc.Live.Count);
            Assert.AreEqual("CZ,r2", 
                string.Join(",", bwslc.Live.Select(l => l.Key.ToString()).OrderBy(n => n)));
        }

        [Test(Description = "Trace until the comparison that gates the jump is encountered.")]
        public void Bwslc_RangeCheck()
        {
            var r1 = Reg(1);
            var r2 = Reg(2);
            var cz = Cc("CZ");

            var b = Given_Block(0x100);
            Given_Instrs(b, m => { m.Assign(cz, m.Cond(m.ISub(r2, 4))); });
            Given_Instrs(b, m => { m.Branch(m.Test(ConditionCode.ULE, cz), Address.Ptr32(0x200), RtlClass.ConditionalTransfer); });

            var b2 = Given_Block(0x200);
            Given_Instrs(b2, m => { m.Assign(r1, m.Shl(r2, 2)); });
            Given_Instrs(b2, m => { m.Goto(m.IAdd(r1, 0x00123400)); });

            graph.Nodes.Add(b);
            graph.Nodes.Add(b2);
            graph.AddEdge(b, b2);

            var bwslc = new BackwardSlicer(b2, host);
            Assert.IsTrue(bwslc.Start());  // indirect jump
            Assert.IsTrue(bwslc.Step());    // shift left
            Assert.IsTrue(bwslc.Step());    // branch
            Assert.IsFalse(bwslc.Step());    // test
            Assert.AreEqual("r2",
                string.Join(",", bwslc.Live.Select(l => l.Key.ToString()).OrderBy(n => n)));

        }
    }
}
