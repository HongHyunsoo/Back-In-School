using UnityEngine;

/// <summary>
/// Marker component used by MapTransitionPortal to place the player
/// at a named destination after transition.
/// </summary>
public class MapTransitionDestination : MonoBehaviour
{
    [Tooltip("Unique destination key in this scene (ex: FLOOR2_STAIRS_IN, CLASSROOM_1_IN).")]
    public string destinationId;
}

