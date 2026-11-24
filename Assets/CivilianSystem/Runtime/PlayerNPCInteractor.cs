// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEngine;

namespace CivilianSystem
{
    /// <summary>
    /// Add to the player. Press the talk key near an NPC to start a conversation:
    /// the NPC stops, faces the player, plays Talk anim, and shows a speech bubble.
    /// </summary>
    public class PlayerNPCInteractor : MonoBehaviour
    {
        public KeyCode talkKey = KeyCode.E;
        public float talkRange = 3f;
        [Tooltip("Seconds the NPC stays in conversation with the player")]
        public float talkDuration = 5f;

        Civilian nearest;

        void Update()
        {
            nearest = FindNearest();

            if (nearest != null && Input.GetKeyDown(talkKey))
                nearest.TalkToPlayer(transform, talkDuration);
        }

        Civilian FindNearest()
        {
            Civilian best = null;
            float bestSqr = talkRange * talkRange;
            var all = Civilian.AllCivilians;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.CanTalkToPlayer) continue;
                float d = (c.transform.position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = c; }
            }
            return best;
        }

        void OnGUI()
        {
            if (nearest != null)
                GUI.Label(new Rect(Screen.width / 2 - 80, Screen.height - 60, 240, 24),
                    $"[{talkKey}] Talk to {nearest.definition.civilianName}");
        }
    }
}
