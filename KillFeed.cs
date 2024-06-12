using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class KillFeed : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI i_Feed;

    public void Setup(string _text)
    {
        i_Feed.text = _text;
        StartCoroutine(DelayDestroy());
    }

    private IEnumerator DelayDestroy()
    {
        Color l_Color = i_Feed.color;
        float l_Timer = 2f;
        while (l_Timer >= 0)
        {
            yield return new WaitForSeconds(0.1f);
            l_Timer -= 0.1f;
            l_Color.a -= 0.05f;
            i_Feed.color = l_Color;
        }
        Destroy(gameObject, 0.1f);
    }
}
