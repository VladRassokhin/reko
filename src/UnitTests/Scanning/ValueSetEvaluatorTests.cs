﻿#region License
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

using NUnit.Framework;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Types;
using Reko.Scanning;
using Reko.UnitTests.Mocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.UnitTests.Scanning
{
    [TestFixture]
    public class ValueSetEvaluatorTests
    {
        private Program program;
        private ProcedureBuilder m;

        [SetUp]
        public void Setup()
        {
            var addr = Address.Ptr32(0x2000);
            m = new ProcedureBuilder();
            program = new Program
            {
                Architecture = m.Architecture,
                SegmentMap = new SegmentMap(
                    Address.Ptr32(0x2000),
                    new ImageSegment(
                        "blob",
                        new MemoryArea(addr, new byte[0x400]),
                        AccessMode.ReadWriteExecute))
            };
        }

        private ValueSet IVS(int stride, long low, long high)
        {
            return new IntervalValueSet(PrimitiveType.Word32, StridedInterval.Create(stride, low, high));
        }

        private ValueSet CVS(params int [] values)
        {
            return new ConcreteValueSet(
                PrimitiveType.Word32,
                values
                    .Select(v => Constant.Create(PrimitiveType.Word32, v))
                    .ToArray());
        }

        [Test]
        public void Vse_Identifier()
        {
            var r1 = m.Reg32("r1", 1);
            var vse = new ValueSetEvaluator(
                program,
                new Dictionary<Expression, ValueSet>(new ExpressionValueComparer())
                {
                    { r1, IVS(4, 0, 20) }
                });
            var vs = r1.Accept(vse);
            Assert.AreEqual("4[0,14]", vs.ToString());
        }

        [Test]
        public void Vse_Sum()
        {
            var r1 = m.Reg32("r1", 1);
            var vse = new ValueSetEvaluator(
                program,
                new Dictionary<Expression, ValueSet>(new ExpressionValueComparer())
                {
                    { r1, IVS(4, 0, 20) }
                });
            var vs = m.IAdd(r1, 9).Accept(vse);
            Assert.AreEqual("4[9,1D]", vs.ToString());
        }

        [Test]
        public void Vse_Load()
        {
            var w = program.CreateImageWriter(Address.Ptr32(0x2000));
            w.WriteUInt32(0x3000);
            w.WriteUInt32(0x3028);
            w.WriteUInt32(0x3008);
            var r1 = m.Reg32("r1", 1);

            var vse = new ValueSetEvaluator(
                program,
                new Dictionary<Expression, ValueSet>(new ExpressionValueComparer())
                {
                    { r1, IVS(4, 0x2000, 0x2008) }
                });
            var vs = m.LoadDw(r1).Accept(vse);
            Assert.AreEqual("[0x00003000,0x00003028,0x00003008]", vs.ToString());
        }

        [Test]
        public void Vse_And()
        {
            var r1 = m.Reg32("r1", 1);
            var vse = new ValueSetEvaluator(
                program,
                new Dictionary<Expression, ValueSet>(new ExpressionValueComparer())
                {
                    { r1, IVS(4, -4000, 4000) }
                });
            var vs = m.And(r1, 0x1F).Accept(vse);
            Assert.AreEqual("1[0,1F]", vs.ToString());
        }

        [Test]
        public void Vse_Shl()
        {
            var r1 = m.Reg32("r1", 1);
            var vse = new ValueSetEvaluator(
                program,
                new Dictionary<Expression, ValueSet>(new ExpressionValueComparer())
                {
                    { r1, IVS(4, -0x40, 0x40) }
                });
            var vs = m.Shl(r1, 2).Accept(vse);
            Assert.AreEqual("10[-100,100]", vs.ToString());
        }

        [Test]
        public void Vse_Cast_Truncate_SingleValue()
        {
            var r1 = m.Reg32("r1", 1);
            var vse = new ValueSetEvaluator(
                program,
                new Dictionary<Expression, ValueSet>(new ExpressionValueComparer())
                {
                    { r1, IVS(0, -0x43F, -0x43F) }
                });
            var vs = m.Cast(PrimitiveType.Byte, r1).Accept(vse);
            Assert.AreEqual("0[-3F,-3F]", vs.ToString());
        }

        [Test]
        public void Vse_Cast_Truncate_Interval()
        {
            var r1 = m.Reg32("r1", 1);
            var vse = new ValueSetEvaluator(
                program,
                new Dictionary<Expression, ValueSet>(new ExpressionValueComparer())
                {
                    { r1, IVS(4, -0x400, 0x400) }
                });
            var vs = m.Cast(PrimitiveType.Byte, r1).Accept(vse);
            Assert.AreEqual("1[0,FF]", vs.ToString());
        }

        [Test]
        public void Vse_Cast_SignExtend()
        {
            var r1 = m.Reg32("r1", 1);
            var vse = new ValueSetEvaluator(
                program,
                new Dictionary<Expression, ValueSet>(new ExpressionValueComparer())
                {
                    { r1, CVS(0x1FF, 0x00, 0x7F) }
                });
            var vs = m.Cast(
                PrimitiveType.Int32,
                m.Cast(PrimitiveType.Byte, r1)).Accept(vse);
            Assert.AreEqual("[-1,0,127]", vs.ToString());
        }

        [Test]
        public void Vse_Add_self()
        {
            var r1 = m.Reg32("r1", 1);
            var vse = new ValueSetEvaluator(
                program,
                new Dictionary<Expression, ValueSet>(new ExpressionValueComparer())
                {
                    { r1, IVS(4, 10, 20) }
                });
            var vs = m.IAdd(r1, r1).Accept(vse);
            Assert.AreEqual("8[14,28]", vs.ToString());
        }

        [Test]
        public void Vse_Add_Mul()
        {
            var r1 = m.Reg32("r1", 1);
            var vse = new ValueSetEvaluator(
                program,
                new Dictionary<Expression, ValueSet>(new ExpressionValueComparer())
                {
                    { r1, CVS(3, 9, 10) }
                });
            var vs = m.IMul(r1, 4).Accept(vse);
            Assert.AreEqual("[0x0000000C,0x00000024,0x00000028]", vs.ToString());
        }
    }
}
