using UnityEngine;

/// <summary>
/// Per-zone music playlist with shuffle and crossfade rules. Consumed by AudioManager.
/// Instances live in ScriptableObjects/Playlists/ and are referenced by playlistId.
/// </summary>
[CreateAssetMenu(fileName = "SO_Playlist_New", menuName = "Game/Audio/MusicPlaylist")]
public class MusicPlaylist : ScriptableObject
{
    public string       playlistId;
    public AudioClip[]  tracks;
    public bool         shuffle;
    public float        crossfadeTime;
}
