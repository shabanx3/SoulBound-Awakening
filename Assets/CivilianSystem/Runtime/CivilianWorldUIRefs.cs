// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEngine;
using UnityEngine.UI;

namespace CivilianSystem
{
    /// <summary>
    /// Sits on the world-UI canvas prefab with the references wired in the Inspector.
    /// An editable starter prefab comes from Tools > Civilian System > Create World UI Prefab.
    /// </summary>
    public class CivilianWorldUIRefs : MonoBehaviour
    {
        [Header("Health")]
        [Tooltip("Root object toggled on hit (bar + text together)")]
        public GameObject healthRoot;
        [Tooltip("Fill image — width or fillAmount scaled by health %")]
        public Image healthFill;
        [Tooltip("Set true if healthFill uses Image type 'Filled' (fillAmount) instead of width scaling")]
        public bool useFillAmount = false;
        [Tooltip("Text showing '75 / 100'")]
        public Text healthText;

        [Header("Speech")]
        [Tooltip("Root object toggled while a dialogue line shows")]
        public GameObject speechRoot;
        public Text speechText;
    }
}
