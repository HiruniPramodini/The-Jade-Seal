using UnityEngine;

namespace JadeSeal.MountainArcher
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class MountainArcherArrow : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float lifeSeconds = 4f;
        private GameObject sender;
        private float damage;
        private bool spent;

        public void Initialize(GameObject source, float power)
        {
            sender = source;
            damage = power;
            Destroy(gameObject, lifeSeconds);
        }

        private void OnCollisionEnter(Collision c)
        {
            if (spent) return;
            if (sender && (c.gameObject == sender || c.transform.IsChildOf(sender.transform))) return;
            spent = true;

            var target = c.gameObject.GetComponentInParent<IMountainArcherDamageable>();
            if (target != null)
            {
                target.TakeDamage(damage);
            }

            // Also check generic Unity SendMessage damage receiver for scene player
            c.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

            Destroy(gameObject);
        }
    }
}
