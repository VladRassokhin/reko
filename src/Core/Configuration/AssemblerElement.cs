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
using System.Configuration;
using System.Text;

namespace Reko.Core.Configuration
{
    public interface AssemblerElement
    {
        string Name { get; }
        string Description { get; }
        string TypeName { get; }
    }

    public class AssemblerElementImpl : AssemblerElement
    {
        /// <summary>
        /// Short, technical name for the assembler.
        /// the assembler.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Human friendly description of the assembler.
        /// the assembler.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The assembly-qualified name for the .NET type that implements
        /// the assembler.
        /// </summary>
        public string TypeName { get; set; }
    }
}
