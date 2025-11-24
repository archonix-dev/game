using Mirror;
using UnityEngine;

namespace Objects
{
    public class RaritySpawner : NetworkBehaviour
    {
        [System.Serializable]
        public class RarityPool
        {
            [Range(0f, 1f)]
            public float weight = 0.25f;
            public Transform[] spawnPoints;
            public GameObject[] prefabs;
        }

        [SerializeField] private RarityPool[] commonPools;
        [SerializeField] private RarityPool[] uncommonPools;
        [SerializeField] private RarityPool[] rarePools;
        [SerializeField] private RarityPool[] veryRarePools;

        private bool hasSpawned;

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (hasSpawned) return;
            SpawnAll();
            hasSpawned = true;
        }

        [Server]
        private void SpawnAll()
        {
            SpawnFromPools(commonPools);
            SpawnFromPools(uncommonPools);
            SpawnFromPools(rarePools);
            SpawnFromPools(veryRarePools);
        }

        [Server]
        private void SpawnFromPools(RarityPool[] pools)
        {
            if (pools == null || pools.Length == 0)
            {
                return;
            }

            foreach (var pool in pools)
            {
                if (pool == null || pool.spawnPoints == null || pool.prefabs == null || pool.prefabs.Length == 0)
                {
                    continue;
                }

                foreach (var point in pool.spawnPoints)
                {
                    if (point == null)
                    {
                        continue;
                    }

                    if (Random.value > Mathf.Clamp01(pool.weight))
                    {
                        continue;
                    }

                    var prefab = pool.prefabs[Random.Range(0, pool.prefabs.Length)];
                    var instance = Instantiate(prefab, point.position, point.rotation);
                    NetworkServer.Spawn(instance);
                }
            }
        }
    }
}

