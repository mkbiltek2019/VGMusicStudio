﻿using Kermalis.VGMusicStudio.Properties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kermalis.VGMusicStudio.Core.NDS.SDAT
{
    internal class Config : Core.Config
    {
        public readonly SDAT SDAT;

        public Config(SDAT sdat)
        {
            if (sdat.INFOBlock.SequenceInfos.NumEntries == 0)
            {
                throw new Exception(Strings.ErrorSDATNoSequences);
            }
            SDAT = sdat;
            IEnumerable<Song> songs = Enumerable.Range(0, sdat.INFOBlock.SequenceInfos.NumEntries)
                .Where(i => sdat.INFOBlock.SequenceInfos.Entries[i] != null)
                .Select(i => new Song(i, sdat.SYMBBlock == null ? i.ToString() : sdat.SYMBBlock.SequenceSymbols.Entries[i]));
            Playlists.Add(new Playlist(Strings.PlaylistMusic, songs));
        }

        public override string GetSongName(long index)
        {
            return SDAT.SYMBBlock == null || index < 0 || index >= SDAT.SYMBBlock.SequenceSymbols.NumEntries
                ? index.ToString()
                : '\"' + SDAT.SYMBBlock.SequenceSymbols.Entries[index] + '\"';
        }
    }
}
