using System.Collections.Generic;

namespace NorthStar.Audio
{
    /// <summary>
    /// Pure, MonoBehaviour-free lookup from a zoneId (and from a playlistId) to a
    /// <see cref="MusicPlaylist"/>. <see cref="AudioManager"/> builds this from its
    /// serialized zone→playlist mapping so that handling a <c>ZoneEnteredEvent</c>
    /// resolves the right playlist to crossfade to.
    ///
    /// <para><c>MusicPlaylist</c> has no zoneId of its own (its schema is playlistId /
    /// tracks / shuffle / crossfadeTime — frozen in INTERFACE.md), so the zone→playlist
    /// association lives here, supplied by the manager's inspector entries. Kept separate
    /// from the manager so resolution is unit-testable in EditMode.</para>
    /// </summary>
    public sealed class ZonePlaylistTable
    {
        private readonly Dictionary<string, MusicPlaylist> _byZone =
            new Dictionary<string, MusicPlaylist>();
        private readonly Dictionary<string, MusicPlaylist> _byPlaylistId =
            new Dictionary<string, MusicPlaylist>();

        /// <summary>
        /// Associate a zoneId with a playlist. Blank zoneId or null playlist is ignored.
        /// A later entry for the same zoneId replaces the earlier one. The playlist is
        /// also indexed by its own <c>playlistId</c> for direct PlayMusic lookups.
        /// </summary>
        public void Add(string zoneId, MusicPlaylist playlist)
        {
            if (playlist == null) return;

            if (!string.IsNullOrEmpty(zoneId))
                _byZone[zoneId] = playlist;

            if (!string.IsNullOrEmpty(playlist.playlistId))
                _byPlaylistId[playlist.playlistId] = playlist;
        }

        /// <summary>Resolve the playlist mapped to a zoneId, or null if none.</summary>
        public MusicPlaylist ResolveByZone(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return null;
            return _byZone.TryGetValue(zoneId, out var pl) ? pl : null;
        }

        /// <summary>Resolve a playlist by its own playlistId, or null if none.</summary>
        public MusicPlaylist ResolveByPlaylistId(string playlistId)
        {
            if (string.IsNullOrEmpty(playlistId)) return null;
            return _byPlaylistId.TryGetValue(playlistId, out var pl) ? pl : null;
        }

        /// <summary>True if a playlist is mapped to the given zoneId.</summary>
        public bool HasZone(string zoneId) =>
            !string.IsNullOrEmpty(zoneId) && _byZone.ContainsKey(zoneId);

        /// <summary>Number of distinct zone mappings.</summary>
        public int ZoneCount => _byZone.Count;
    }
}
