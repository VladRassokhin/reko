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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Scanning
{
    public abstract class ValueSet
    {
        public abstract ValueSet Add(ValueSet right);
        public abstract ValueSet Add(Constant right);
    }

    public class IntervalValueSet : ValueSet
    {
        public StridedInterval SI;

        public IntervalValueSet(StridedInterval si)
        {
            this.SI = si;
        }

        public override ValueSet Add(ValueSet right)
        {
            throw new NotImplementedException();
        }

        public override ValueSet Add(Constant right)
        {
            long v = right.ToInt64();
            return new IntervalValueSet(
                StridedInterval.Create(
                    SI.Stride,
                    SI.Low + v,
                    SI.High + v));
        }

        public override string ToString()
        {
            return SI.ToString();
        }
    }

    public class ConcreteValueSet : ValueSet
    {
        public override ValueSet Add(Constant right)
        {
            throw new NotImplementedException();
        }

        public override ValueSet Add(ValueSet right)
        {
            throw new NotImplementedException();
        }
    }
}
