using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhoneSystem : MonoBehaviour
{
    public static PhoneSystem Instance { get; private set; }

    [Header("Assign in Inspector")]
    public GameObject phoneUIPrefab;   // �� UI ������
    private GameObject phoneUIInstance;

    private void Awake()
    {
        // �̱��� + �� �̵��ص� ����
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Open()
    {
        if (phoneUIInstance == null)
        {
            if (phoneUIPrefab == null)
            {
                Debug.LogError("[PhoneSystem] phoneUIPrefab ? ????? ???? ?????. ??? UI? ??? ? ????.");
                return;
            }

            phoneUIInstance = Instantiate(phoneUIPrefab);
            DontDestroyOnLoad(phoneUIInstance); // �� UI�� ����
        }
        phoneUIInstance.SetActive(true);
    }

    public void Close()
    {
        if (phoneUIInstance != null)
            phoneUIInstance.SetActive(false);
    }

    public bool IsOpen => phoneUIInstance != null && phoneUIInstance.activeSelf;
}

