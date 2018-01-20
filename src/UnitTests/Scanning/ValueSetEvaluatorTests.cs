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
            program = new Program();
            m = new ProcedureBuilder();
        }

        private ValueSet VS(int stride, long low, long high)
        {
            return new IntervalValueSet(StridedInterval.Create(stride, low, high));
        }

        [Test]
        public void Vse_Identifier()
        {
            var r1 = m.Reg32("r1", 1);
            var vse = new ValueSetEvaluator(
                program,
                new Dictionary<Expression, ValueSet>(new ExpressionValueComparer())
                {
                    { r1, VS(4, 0, 20) }
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
                    { r1, VS(4, 0, 20) }
                });
            var vs = m.IAdd(r1, 9).Accept(vse);
            Assert.AreEqual("4[9,1D]", vs.ToString());
        }
    }
}
