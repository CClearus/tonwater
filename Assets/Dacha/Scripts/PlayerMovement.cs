using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] float speed = 5f;

    void Update()
    {
        if (!IsOwner) return; // only control your own player

        var kb = Keyboard.current;
        if (kb == null) return;
        float x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
        float z = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
        transform.position += new Vector3(x, 0, z) * speed * Time.deltaTime;
    }
}
