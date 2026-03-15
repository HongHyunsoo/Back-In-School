using System.Collections;
using UnityEngine;

public class CutsceneCommandRunner : MonoBehaviour
{
    [Header("Optional: pass objects prefab folder")]
    public string passPrefabFolder = "Prefabs";

    public CharacterRegistry characterRegistry; // 아래 4)에서 만들거
    public AudioSource sfxSource; // 있으면 연결

    public IEnumerator Execute(string rawText)
    {
        var tags = TagParser.Extract(rawText);

        foreach (var tag in tags)
        {
            switch (tag.cmd)
            {
                case "wait":
                    yield return new WaitForSeconds(ParseF(tag.args, 0));
                    break;

                case "anim":
                    {
                        // [anim:SOWRD,Stretch]
                        var ch = characterRegistry.Get(tag.args[0]);
                        ch.Animator.SetTrigger(tag.args[1]);
                        break;
                    }

                case "move":
                    {
                        // [move:PLAYER,3.2,1.0,0.6]
                        var ch = characterRegistry.Get(tag.args[0]);
                        var x = ParseF(tag.args, 1);
                        var y = ParseF(tag.args, 2);
                        var t = ParseF(tag.args, 3);
                        yield return ch.MoveTo(new Vector2(x, y), t);
                        break;
                    }

                case "door":
                    {
                        // [door:ClassDoor,Close]
                        var go = GameObject.Find(tag.args[0]);
                        var anim = go.GetComponent<Animator>();
                        anim.SetTrigger(tag.args[1]);
                        break;
                    }

                case "pass":
                    {
                        // [pass:Cat,-8,1,8,1,2.0]
                        var prefabName = tag.args[0];
                        var start = new Vector2(ParseF(tag.args, 1), ParseF(tag.args, 2));
                        var end = new Vector2(ParseF(tag.args, 3), ParseF(tag.args, 4));
                        var dur = ParseF(tag.args, 5);
                        yield return SpawnAndPass(prefabName, start, end, dur);
                        break;
                    }

                case "sfx":
                    {
                        // [sfx:door]
                        if (sfxSource != null)
                        {
                            var clip = Resources.Load<AudioClip>($"SFX/{tag.args[0]}");
                            if (clip != null) sfxSource.PlayOneShot(clip);
                        }
                        break;
                    }
            }
        }
    }

    IEnumerator SpawnAndPass(string prefabName, Vector2 start, Vector2 end, float dur)
    {
        var prefab = Resources.Load<GameObject>($"{passPrefabFolder}/{prefabName}");
        if (prefab == null) yield break;

        var go = Instantiate(prefab);
        go.transform.position = start;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, dur);
            go.transform.position = Vector2.Lerp(start, end, t);
            yield return null;
        }
        Destroy(go);
    }

    float ParseF(string[] a, int idx) => float.Parse(a[idx], System.Globalization.CultureInfo.InvariantCulture);
}
