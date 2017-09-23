﻿#region License
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Reko.Core
{
    /// <summary>
    /// Represents a bit range within a register as two shorts.
    /// </summary>
    public struct BitRange
    {
        public static readonly BitRange Empty = new BitRange(0, 0);

        public BitRange(int lsb, int msb)
        {
            this.Lsb = (short)lsb;
            this.Msb = (short)msb;
        }

        public short Lsb { get; private set; }
        public short Msb { get; private set; }

        public int Extent
        {
            get { return Math.Max(Msb - Lsb, 0); }
        }

        public bool IsEmpty
        {
            get { return Lsb >= Msb; }
        }

        public static BitRange operator | (BitRange a, BitRange b)
        {
            if (a.IsEmpty)
                return b;
            if (b.IsEmpty)
                return a;
            return new BitRange(
                Math.Min(a.Lsb, b.Lsb),
                Math.Max(a.Msb, b.Msb));
        }

        public static BitRange operator & (BitRange a, BitRange b)
        {
            return new BitRange(
                Math.Max(a.Lsb, b.Lsb),
                Math.Min(a.Msb, b.Msb));
        }


        public static BitRange operator -(BitRange a, BitRange b)
        {
            var d = a & b;
            if (d.IsEmpty)
                return a;
            if (d.Lsb == a.Lsb)
            {
                return new BitRange(d.Msb, a.Msb);
            }
            else if (d.Msb == a.Msb)
            {
                return new BitRange(a.Lsb, d.Lsb);
            }
            return a;
        }

        public static bool operator ==(BitRange a, BitRange b)
        {
            return a.Lsb == b.Lsb && a.Msb == b.Msb;
        }

        public static bool operator !=(BitRange a, BitRange b)
        {
            return a.Lsb != b.Lsb || a.Msb != b.Msb;
        }

        public override bool Equals(object obj)
        {
            if (obj is BitRange)
            {
                var that = (BitRange)obj;
                if (this.IsEmpty)
                    return that.IsEmpty;
                return this.Msb == that.Msb && this.Lsb == that.Lsb;
            }
            return false;
        }

        public override int GetHashCode()
        {
            if (this.IsEmpty)
                return 0;
            return Lsb.GetHashCode() ^ 17 * Msb.GetHashCode();
        }

        public override string ToString()
        {
            if (IsEmpty)
                return "[]";
            else
                return string.Format("[{0}..{1}]", Lsb, Msb - 1);
        }
    }
}