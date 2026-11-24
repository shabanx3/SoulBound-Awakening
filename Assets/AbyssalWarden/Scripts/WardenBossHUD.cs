using UnityEngine;
namespace AbyssalWarden
{
    public class WardenBossHUD : MonoBehaviour
    {
        public AbyssalWardenBoss boss;
        public bool showOnlyWhenEngaged=true;
        public float widthFraction=.56f;
        [Tooltip("After death the bar shows DEFEATED for this long, then fades out.")]
        public float defeatedDisplaySeconds=2.5f;
        public float fadeSeconds=1f;

        float deathTime=-1f;

        void OnGUI()
        {
            if(boss==null || WardenCinematicRig.Active) return;
            float alpha=1f;
            if(boss.IsDead)
            {
                if(deathTime<0f) deathTime=Time.time;
                float since=Time.time-deathTime;
                if(since>defeatedDisplaySeconds+fadeSeconds) return; // gone once he is defeated
                if(since>defeatedDisplaySeconds) alpha=1f-(since-defeatedDisplaySeconds)/fadeSeconds;
            }
            else
            {
                deathTime=-1f;
                if(showOnlyWhenEngaged&&!boss.IsEngaged) return;
            }

            float width=Screen.width*widthFraction, x=(Screen.width-width)*.5f, y=Screen.height-110f;
            var title=new GUIStyle(GUI.skin.label) {fontSize=Mathf.Clamp(Screen.height/47,15,30),alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
            title.normal.textColor=new Color(.93f,.85f,.75f);
            var previous=GUI.color;
            GUI.color=new Color(1f,1f,1f,alpha);
            string label=boss.IsDead?boss.bossName+"  —  DEFEATED":boss.bossName+(boss.PhaseTwo?"  —  ASCENDED":"");
            GUI.Label(new Rect(x,y-42,width,34),label,title);
            GUI.Box(new Rect(x,y,width,19),GUIContent.none);
            GUI.color=boss.PhaseTwo?new Color(.85f,.24f,.16f,alpha):new Color(.73f,.39f,.15f,alpha);
            GUI.DrawTexture(new Rect(x+3,y+3,Mathf.Max(0,width-6)*boss.HealthNormalized,13),Texture2D.whiteTexture);
            GUI.color=previous;
        }
    }
}
