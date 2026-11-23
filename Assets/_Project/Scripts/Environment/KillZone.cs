using Stitches.Player;
using UnityEngine;

namespace Stitches.Environment
{
    public class KillZone : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                PlayerController player = collision.GetComponent<PlayerController>();
                if (player != null)
                {
                    player.Die();
                }
            }
        }
    }
}
