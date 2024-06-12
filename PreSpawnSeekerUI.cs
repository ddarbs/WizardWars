using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class PreSpawnSeekerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI i_TextOne, i_TextTwo, i_TextThree;

    [SerializeField] private Image i_SmileyFace;
    
    [SerializeField] private Color i_Color, i_SmileyColor;
    
    private void OnEnable()
    {
        i_TextOne.color = i_Color;
        i_TextTwo.color = i_Color;
        i_TextThree.color = i_Color;
        i_TextThree.text = "prepare to <color=red>???????</color>";
        i_SmileyFace.color = i_SmileyColor;
        StartCoroutine(TextCinematic());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator TextCinematic()
    {
        Color l_Color = i_Color;
        while (i_TextOne.color.a < 1)
        {
            yield return new WaitForSeconds(0.1f);
            l_Color.a += 0.05f;
            i_TextOne.color = l_Color;
        }
        while (i_TextOne.color.a > 0)
        {
            yield return new WaitForSeconds(0.1f);
            l_Color.a -= 0.05f;
            i_TextOne.color = l_Color;
        }
        while (i_TextTwo.color.a < 1)
        {
            yield return new WaitForSeconds(0.1f);
            l_Color.a += 0.05f;
            i_TextTwo.color = l_Color;
        }
        while (i_TextTwo.color.a > 0)
        {
            yield return new WaitForSeconds(0.1f);
            l_Color.a -= 0.05f;
            i_TextTwo.color = l_Color;
        }
        while (i_TextThree.color.a < 1)
        {
            yield return new WaitForSeconds(0.1f);
            l_Color.a += 0.05f;
            i_TextThree.color = l_Color;
        }
        StartCoroutine(FadeSmiley());
        StartCoroutine(RedactLetters());
        while (i_TextThree.color.a > 0)
        {
            yield return new WaitForSeconds(0.1f);
            l_Color.a -= 0.05f;
            i_TextThree.color = l_Color;
        }
    }

    private IEnumerator RedactLetters() // decrypt now, flipped it around
    {
        yield return new WaitForSeconds(1f);
        string l_Redact = "???????";
        int l_Random = 0;
        int l_Tries = 0;
        while (l_Tries <= 200)
        {
            l_Random = Random.Range(0, l_Redact.Length);
            if (l_Redact[l_Random] != '?')
            {
                l_Tries++;
                continue;
            }

            l_Redact = l_Random switch
            {
                0 => l_Redact.Remove(l_Random, 1).Insert(l_Random, "d"),
                1 => l_Redact.Remove(l_Random, 1).Insert(l_Random, "e"),
                2 => l_Redact.Remove(l_Random, 1).Insert(l_Random, "s"),
                3 => l_Redact.Remove(l_Random, 1).Insert(l_Random, "c"),
                4 => l_Redact.Remove(l_Random, 1).Insert(l_Random, "e"),
                5 => l_Redact.Remove(l_Random, 1).Insert(l_Random, "n"),
                6 => l_Redact.Remove(l_Random, 1).Insert(l_Random, "d"),
                _ => l_Redact
            };
            i_TextThree.text = $"prepare to <color=red>{l_Redact}</color>";
            if (l_Redact == "descend")
            {
                break;
            }
            l_Tries++;
            
            
            yield return new WaitForSeconds(0.25f);
        }
    }

    private IEnumerator FadeSmiley()
    {
        yield return new WaitForSeconds(0.5f);
        Color l_Color = i_SmileyColor;
        while (i_SmileyFace.color.a > 0)
        {
            yield return new WaitForSeconds(0.1f);
            l_Color.a -= 0.01f;
            i_SmileyFace.color = l_Color;
        }
    }
}
