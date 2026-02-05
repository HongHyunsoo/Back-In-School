using System.Collections;
using UnityEngine;

public class CharacterActor : MonoBehaviour
{
    public string characterId;        // "PLAYER", "SOWRD" µî
    public Animator Animator;

    void Reset()
    {
        Animator = GetComponentInChildren<Animator>();
    }

    public IEnumerator MoveTo(Vector2 target, float duration)
    {
        var start = (Vector2)transform.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            transform.position = Vector2.Lerp(start, target, t);
            yield return null;
        }
        transform.position = target;
    }
}
