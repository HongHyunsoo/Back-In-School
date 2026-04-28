using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueAnimationClipCatalog", menuName = "Back In School/Dialogue Animation Clip Catalog")]
public class DialogueAnimationClipCatalogAsset : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string key;
        public AnimationClip clip;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;

    public void ReplaceEntries(List<Entry> nextEntries)
    {
        entries = nextEntries ?? new List<Entry>();
    }
}
