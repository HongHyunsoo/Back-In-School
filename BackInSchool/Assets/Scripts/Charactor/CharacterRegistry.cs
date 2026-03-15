using System.Collections.Generic;
using UnityEngine;

public class CharacterRegistry : MonoBehaviour
{
    Dictionary<string, CharacterActor> map;

    void Awake()
    {
        map = new Dictionary<string, CharacterActor>();
        foreach (var actor in FindObjectsOfType<CharacterActor>())
        {
            if (!string.IsNullOrEmpty(actor.characterId))
                map[actor.characterId] = actor;
        }
    }

    public CharacterActor Get(string id)
    {
        if (map.TryGetValue(id, out var actor)) return actor;
        Debug.LogError($"Character not found: {id}");
        return null;
    }
}
