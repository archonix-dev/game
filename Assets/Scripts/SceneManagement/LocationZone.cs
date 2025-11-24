using System;
using Mirror;
using UnityEngine;

/// <summary>
/// Триггерная зона, задающая название локации.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LocationZone : MonoBehaviour
{
    public static event Action<string> OnLocalPlayerEnterZone;

    [SerializeField] private string locationName = "Default";

    Collider zoneCollider;

    void Awake()
    {
        zoneCollider = GetComponent<Collider>();

        if (zoneCollider != null && !zoneCollider.isTrigger)
        {
            zoneCollider.isTrigger = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        NotifyIfLocalPlayer(other);
    }

    void OnTriggerStay(Collider other)
    {
        NotifyIfLocalPlayer(other);
    }

    void NotifyIfLocalPlayer(Collider other)
    {
        if (string.IsNullOrWhiteSpace(locationName))
        {
            return;
        }

        if (!TryGetLocalPlayer(other, out _))
        {
            return;
        }

        OnLocalPlayerEnterZone?.Invoke(locationName);
    }

    bool TryGetLocalPlayer(Collider other, out NetworkIdentity identity)
    {
        identity = other.GetComponentInParent<NetworkIdentity>() ?? other.GetComponent<NetworkIdentity>();

        if (identity == null)
        {
            return false;
        }

        return identity.isLocalPlayer;
    }
}

