﻿#region License
/* 
 * Copyright (C) 1999-2015 John Källén.
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
using System.Globalization;
using System.Linq;
using System.Text;
using Reko.Core;
using System.IO;

namespace Reko.ImageLoaders.MzExe
{
    public class PeResourceLoader
    {
        const uint RT_BITMAP = 2;
        const uint RT_ICON = 3;
        const uint RT_MENU = 4;
        const uint RT_DIALOG = 5;
        const uint RT_STRING = 6;
        const uint RT_ACCELERATOR = 9;
        const uint RT_GROUP_ICON = 14;
        const uint RT_VERSION = 16;

        private LoadedImage imgLoaded;
        private uint rvaResources;

        public PeResourceLoader(LoadedImage imgLoaded, uint rvaResources)
        {
            this.imgLoaded = imgLoaded;
            this.rvaResources = rvaResources;
        }

        public List<ProgramResource> Load()
        {
            var rsrcSection = new LeImageReader(this.imgLoaded, rvaResources);
            var rdr = rsrcSection.Clone();

            return ReadResourceDirectory(rdr);
        }

        public List<ProgramResource> ReadResourceDirectory(ImageReader rdr)
        {
            const uint DIR_MASK = 0x80000000;
            var flags = rdr.ReadUInt32();
            var date = rdr.ReadUInt32();
            var version = rdr.ReadUInt32();
            var cNameEntries = rdr.ReadUInt16();
            var cIdEntries = rdr.ReadUInt16();
            var entries = new List<ProgramResource>();
            for (int i = 0; i < cNameEntries; ++i)
            {
                var rvaName = rdr.ReadUInt32();
                var rvaEntry = rdr.ReadUInt32();
                var subRdr = new LeImageReader(imgLoaded, rvaResources + (rvaEntry & ~DIR_MASK));
                if ((rvaEntry & DIR_MASK) == 0)
                    throw new BadImageFormatException();
                var e = new ProgramResourceGroup
                {
                    Name = ReadResourceString(rvaName),
                };
                e.Resources.AddRange(ReadNameDirectory(subRdr, 0));
                entries.Add(e);
            }
            for (int i = 0; i < cIdEntries; ++i)
            {
                var id = rdr.ReadUInt32();
                var rvaEntry = rdr.ReadUInt32();
                var subRdr = new LeImageReader(imgLoaded, rvaResources + (rvaEntry & ~DIR_MASK));
                if ((rvaEntry & DIR_MASK) == 0)
                    throw new BadImageFormatException();
                var e = new ProgramResourceGroup
                {
                    Name = GenerateResourceName(id),
                };
                e.Resources.AddRange(ReadNameDirectory(subRdr, id));
                entries.Add(e);
            }
            return entries;
        }

        public List<ProgramResource> ReadNameDirectory(ImageReader rdr, uint resourceType)
        {
            const uint DIR_MASK = 0x80000000;
            var flags = rdr.ReadUInt32();
            var date = rdr.ReadUInt32();
            var version = rdr.ReadUInt32();
            var cNameEntries = rdr.ReadUInt16();
            var cIdEntries = rdr.ReadUInt16();
            var entries = new List<ProgramResource>();
            for (int i = 0; i < cNameEntries; ++i)
            {
                var rvaName = rdr.ReadUInt32();
                var rvaEntry = rdr.ReadUInt32();
                var subRdr = new LeImageReader(imgLoaded, rvaResources + (rvaEntry & ~DIR_MASK));
                if ((rvaEntry & DIR_MASK) == 0)
                    throw new BadImageFormatException();
                var e = new ProgramResourceGroup
                {
                    Name = ReadResourceString(rvaName),
                };
                e.Resources.AddRange(ReadLanguageDirectory(subRdr, resourceType, e.Name));
                entries.Add(e);
            }
            for (int i = 0; i < cIdEntries; ++i)
            {
                var id = rdr.ReadUInt32();
                var rvaEntry = rdr.ReadUInt32();
                var subRdr = new LeImageReader(imgLoaded, rvaResources + (rvaEntry & ~DIR_MASK));
                if ((rvaEntry & DIR_MASK) == 0)
                    throw new BadImageFormatException();
                var e = new ProgramResourceGroup
                {
                    Name = id.ToString(),
                };
                e.Resources.AddRange(ReadLanguageDirectory(subRdr, resourceType, e.Name));
                entries.Add(e);
            }
            return entries;
        }

        public List<ProgramResource> ReadLanguageDirectory(ImageReader rdr, uint resourceType, string resourceId)
        {
            const uint DIR_MASK = 0x80000000;
            var flags = rdr.ReadUInt32();
            var date = rdr.ReadUInt32();
            var version = rdr.ReadUInt32();
            var cNameEntries = rdr.ReadUInt16();
            var cIdEntries = rdr.ReadUInt16();
            var entries = new List<ProgramResource>();
            for (int i = 0; i < cNameEntries; ++i)
            {
                var rvaName = rdr.ReadUInt32();
                var rvaEntry = rdr.ReadUInt32();
                var subRdr = new LeImageReader(imgLoaded, rvaResources + (rvaEntry & ~DIR_MASK));
                if ((rvaEntry & DIR_MASK) != 0)
                    throw new BadImageFormatException();
                entries.Add(ReadResourceEntry(subRdr, resourceId, ReadResourceString(rvaName), resourceType));
            }
            for (int i = 0; i < cIdEntries; ++i)
            {
                var id = rdr.ReadUInt32();
                var rvaEntry = rdr.ReadUInt32();
                var subRdr = new LeImageReader(imgLoaded, rvaResources + (rvaEntry & ~DIR_MASK));
                if ((rvaEntry & DIR_MASK) != 0)
                    throw new BadImageFormatException();
                entries.Add(ReadResourceEntry(subRdr, resourceId, id.ToString(), resourceType));
            }
            return entries;
        }

        public ProgramResourceInstance ReadResourceEntry(ImageReader rdr, string resourceId, string langId, uint resourceType)
        {
            var rvaData = rdr.ReadUInt32();
            var size = rdr.ReadUInt32();
            var codepage = rdr.ReadUInt32();
            var padding = rdr.ReadUInt32();
            var abResource = new byte[size];
            Array.Copy(imgLoaded.Bytes, (int)rvaData, abResource, 0, abResource.Length);

            if (resourceType == RT_BITMAP)
            {
                abResource = PostProcessBitmap(abResource);
            }

            string localeName = GetLocaleName(langId);
            return new ProgramResourceInstance
            {
                Name = string.Format("{0}:{1}", resourceId, localeName),
                Type = GetResourceType(resourceType),
                Bytes = abResource,
            };
        }

        private string GetLocaleName(string langId)
        {
            int localeId;
            if (Int32.TryParse(langId, out localeId))
            {
                var ci = CultureInfo.GetCultureInfo(localeId);
                return ci.EnglishName;
            }
            else
            {
                return langId;
            }
        }

        /// <summary>
        /// Rebuild the BITMAPFILEHEADER structure, which isn't present in
        /// resource files.
        /// </summary>
        /// <param name="abResource">DIB image (BITMAPINFOHEADER + bitmap data)</param>
        /// <returns>A BITMAPFILEHEADER</returns>
        private byte[] PostProcessBitmap(byte[] abResource)
        {
            var stm = new MemoryStream();
            var bw = new BinaryWriter(stm); // Always writes little-endian.

            bw.Write('B');
            bw.Write('M');
            bw.Write(14 + abResource.Length);
            bw.Write(0);
            bw.Write(14);
            bw.Write(abResource, 0, abResource.Length);
            bw.Flush();
            return stm.ToArray();
        }

        private string GenerateResourceName(uint id)
        {
            switch (id)
            {
            case RT_BITMAP: return "BITMAP";
            case RT_ICON: return "ICON";
            case RT_MENU: return "MENU";
            case RT_DIALOG: return "DIALOG";
            case RT_STRING: return "STRING";
            case RT_ACCELERATOR: return "ACCELERATOR";
            case RT_GROUP_ICON: return "GROUP_ICON";
            case RT_VERSION: return "VERSION";
            default: return id.ToString();
            }
        }

        public string GetResourceType(uint resourceType)
        {
            switch (resourceType)
            {
            case RT_BITMAP: return "Windows.BMP";
            default: return "";
            }
        }

        public string ReadResourceString(uint rva)
        {
            var rdr = new LeImageReader(imgLoaded, rva);
            var len = rdr.ReadLeInt16();
            var abStr = rdr.ReadBytes(len);
            return Encoding.ASCII.GetString(abStr);
        }

    }
}