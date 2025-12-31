using Unity.Netcode.Components; // Necesario
using UnityEngine;

// Este script permite que el Cliente (tú) le diga al Servidor dónde está.
// Es ideal para FPS y juegos de acción rápida.
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    // Esta función le dice a Unity: "¿Quién tiene permiso para mover esto?"
    // Al devolver 'false', le decimos que NO es exclusivo del servidor.
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}