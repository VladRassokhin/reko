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

using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Lib;
using Reko.Core.Operators;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Scanning
{
    public abstract class ValueSet
    {
        public ValueSet(DataType dt)
        {
            this.DataType = dt;
        }

        public DataType DataType { get; }
        public abstract IEnumerable<Constant> Values { get; }

        public abstract ValueSet Add(ValueSet right);
        public abstract ValueSet Add(Constant right);
        public abstract ValueSet And(Constant cRight);
        public abstract ValueSet IMul(Constant cRight);
        public abstract ValueSet Shl(Constant cRight);
        public abstract ValueSet SignExtend(DataType dataType);

        public abstract ValueSet Truncate(DataType dt);

    }

    public class IntervalValueSet : ValueSet
    {
        public StridedInterval SI;

        public IntervalValueSet(DataType dt, StridedInterval si) : base(dt)
        {
            this.SI = si;
        }

        public override IEnumerable<Constant> Values
        {
            get
            {
                if (SI.Stride < 0)
                    yield break;
                else if (SI.Stride == 0)
                    yield return Constant.Create(DataType, SI.Low);
                else
                {
                    long v = SI.Low; 
                    while (v <= SI.High)
                    {
                        yield return Constant.Create(DataType, v);
                        if (v == SI.High)
                            yield break;
                        v += SI.Stride;
                    }
                }
            }
        }

        public override ValueSet Add(ValueSet right)
        {
            throw new NotImplementedException();
        }

        public override ValueSet Add(Constant right)
        {
            long v = right.ToInt64();
            return new IntervalValueSet(
                this.DataType,
                StridedInterval.Create(
                    SI.Stride,
                    SI.Low + v,
                    SI.High + v));
        }

        public override ValueSet And(Constant right)
        {
            long v = right.ToInt64();
            return new IntervalValueSet(
                this.DataType,
                StridedInterval.Create(1, 0, v));
        }

        public override ValueSet IMul(Constant cRight)
        {
            long v = cRight.ToInt64();
            return new IntervalValueSet(
                this.DataType,
                StridedInterval.Create(
                    SI.Stride * (int)v,
                    SI.Low * v,
                    SI.High * v));
        }

        public override ValueSet Shl(Constant cRight)
        {
            int v = (int) cRight.ToInt64();
            return new IntervalValueSet(
                this.DataType,
                StridedInterval.Create(
                    SI.Stride << v,
                    SI.Low << v,
                    SI.High << v));
        }

        public override ValueSet SignExtend(DataType dataType)
        {
            throw new NotImplementedException();
        }

        public override ValueSet Truncate(DataType dt)
        {
            if (SI.Stride < 0)
                return this;

            var mask = (1 << dt.BitSize) - 1;
            StridedInterval siNew;
            if (SI.Low == SI.High)
            {
                siNew = StridedInterval.Constant(
                    Constant.Create(dt, SI.Low & mask));
            }
            else
            {
                siNew = StridedInterval.Create(
                    1, 0, mask);
            }
            return new IntervalValueSet(dt, siNew);
        }

        public override string ToString()
        {
            return SI.ToString();
        }
    }

    public class ConcreteValueSet : ValueSet
    {
        private Constant[] values;

        public ConcreteValueSet(DataType dt, Constant[] values) : base(dt)
        {
            this.values = values;
        }

        public override IEnumerable<Constant> Values
        {
            get { return values; }
        }

        private ConcreteValueSet Map(DataType dt, Func<Constant,Constant> map)
        {
            return new ConcreteValueSet(
                dt,
                values.Select(map).ToArray());
        }

        public override ValueSet Add(Constant right)
        {
            throw new NotImplementedException();
        }

        public override ValueSet Add(ValueSet right)
        {
            throw new NotImplementedException();
        }

        public override ValueSet And(Constant right)
        {
            throw new NotImplementedException();
        }

        public override ValueSet IMul(Constant cRight)
        {
            return Map(DataType, v => Operator.IMul.ApplyConstants(v, cRight));
        }

        public override ValueSet Shl(Constant cRight)
        {
            throw new NotImplementedException();
        }

        public override ValueSet SignExtend(DataType dt)
        {
            int bits = this.DataType.BitSize;
            return Map(dt, v => Constant.Create(
                dt,
                Bits.SignExtend(v.ToUInt64(), bits)));
        }

        public override ValueSet Truncate(DataType dt)
        {
            var mask = (1L << dt.BitSize) - 1;
            return Map(
                dt, 
                v => Constant.Create(dt, v.ToInt64() & mask));
        }

        public override string ToString()
        {
            return $"[{string.Join(",", values.AsEnumerable())}]";
        }
    }
}
