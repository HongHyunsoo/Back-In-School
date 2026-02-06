#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

public class FlowDebugController : MonoBehaviour
{
    [Header("Keys")]
    public KeyCode skipKey = KeyCode.F9;
    public KeyCode jumpKey = KeyCode.F10;

    [Header("Jump Target")]
    public int jumpDay = 1;
    public int jumpStep = 0;
    public int jumpPenalty = 0;

    void Awake()
{
    if (transform.parent != null)
        transform.SetParent(null);

    DontDestroyOnLoad(gameObject);
}


    void Update()
    {
        var fm = FlowManager.Instance;
        if (fm == null) return;

        // F9: 현재 이벤트 스킵
        if (Input.GetKeyDown(skipKey))
        {
            fm.DebugSkip(0);
        }

        // F10: 지정한 Day/Step으로 점프
        if (Input.GetKeyDown(jumpKey))
        {
            fm.DebugJump(jumpDay, jumpStep, jumpPenalty);
        }
    }

 

}
#endif
