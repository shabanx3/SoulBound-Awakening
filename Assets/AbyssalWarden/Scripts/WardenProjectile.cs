using UnityEngine;
namespace AbyssalWarden
{
    public class WardenProjectile : MonoBehaviour
    {
        Transform owner; Vector3 velocity; float damage; LayerMask layers, obstacles; bool initialized;
        GameObject impactEffect;
        public void Initialize(Transform source,Vector3 direction,float speed,float hitDamage,float lifetime,LayerMask hitLayers)
            => Initialize(source,direction,speed,hitDamage,lifetime,hitLayers,0,null);
        // obstacleLayers: what else stops the projectile (ground, walls). impact: spawned where it stops.
        public void Initialize(Transform source,Vector3 direction,float speed,float hitDamage,float lifetime,LayerMask hitLayers,LayerMask obstacleLayers,GameObject impact)
        {
            owner=source; velocity=direction.normalized*speed; damage=hitDamage; layers=hitLayers; obstacles=obstacleLayers; impactEffect=impact; initialized=true;
            WardenVFX.StripMovers(gameObject);
            Destroy(gameObject,lifetime);
        }
        void Update()
        {
            if(!initialized) return;
            float distance=velocity.magnitude*Time.deltaTime;
            if(Physics.SphereCast(transform.position,.24f,velocity.normalized,out RaycastHit hit,distance,layers|obstacles,QueryTriggerInteraction.Ignore))
            {
                if(owner!=null && hit.transform.IsChildOf(owner)) {transform.position+=velocity*Time.deltaTime; return;}
                var target=hit.collider.GetComponentInParent<IWardenDamageable>();
                if(target!=null && (layers.value & (1<<hit.collider.gameObject.layer))!=0) target.ReceiveDamage(damage,owner);
                WardenVFX.Spawn(impactEffect,hit.point,Quaternion.LookRotation(hit.normal),2.5f);
                WardenVFX.StopAndDestroy(gameObject,.6f);
                initialized=false;
                return;
            }
            transform.position+=velocity*Time.deltaTime;
        }
    }
}
