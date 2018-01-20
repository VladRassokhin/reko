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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Reko.Core.Lib
{
    /// <summary>
    /// Bit manipulation methods.
    /// </summary>
    public static class Bits
    {
        public static bool IsSingleBitSet(uint w)
        {
            return w != 0 && (w & (w - 1)) == 0;
        }

        public static bool IsEvenPowerOfTwo(int n)
        {
            return n != 0 && (n & (n - 1)) == 0;
        }

        /// <summary>
        /// Sign-extend the <paramref name="b"/>-bit number 
        /// <paramref name="w"/>.
        /// </summary>
        /// <param name="w"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static ulong SignExtend(ulong w, int b)
        {
            ulong r;      // resulting sign-extended number
            ulong m = 1LU << (b - 1); // mask can be pre-computed if b is fixed
            w = w & ((1LU << b) - 1);  // (Skip this if bits in x above position b are already zero.)
            r = (w ^ m) - m;
            return r;
        }

        public static ulong Mask(int lsb, int bitsize)
        {
            return ((1ul << bitsize) - 1) << lsb;
        }

        public static int BitCount(ulong u)
        {
            u = u - ((u >> 1) & 0x5555555555555555UL);
            u = (u & 0x3333333333333333UL) + ((u >> 2) & 0x3333333333333333UL);
            u = (u & 0x0F0F0F0F0F0F0F0FUL) + ((u >> 4) & 0x0F0F0F0F0F0F0F0FUL);
            u = (u & 0x00FF00FF00FF00FFUL) + ((u >> 8) & 0x00FF00FF00FF00FFUL);
            u = (u & 0x0000FFFF0000FFFFUL) + ((u >> 16) & 0x0000FFFF0000FFFFUL);
            u = (u & 0x00000000FFFFFFFFUL) + ((u >> 32) & 0x0000000FFFFFFFFUL);
            return (int)u;
        }
    }
}
