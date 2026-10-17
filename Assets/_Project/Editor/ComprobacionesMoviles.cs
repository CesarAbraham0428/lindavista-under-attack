using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Regresión de multitáctil, cancelación y tamaño de los controles reales.</summary>
public static class ComprobacionesMoviles
{
    private const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Lindavista/Comprobar controles móviles (Play Mode)")]
    public static void Comprobar() => Ejecutar();

    public static string Ejecutar()
    {
        Exigir(EditorApplication.isPlaying && !GameFlowController.IsPaused &&
            !GameFlowController.IsMatchEnded && !PistolUpgradeShop.IsOpen,
            "Abrir Gameplay o Testing en Play Mode, después de la introducción.");
        var hud = UnityEngine.Object.FindFirstObjectByType<MobileControlsHUD>();
        Exigir(hud != null, "Existe HUD táctil.");
        Exigir(hud.PuedeRecibirEntrada, "Jugador preparado y controles desbloqueados.");
        bool visibleAnterior = (bool)Campo(hud, "vistaSolicitada");
        typeof(MobileControlsHUD).GetMethod("SetVisible", Privado).Invoke(hud, new object[] { true });
        var movimiento = (PlayerMovement)Campo(hud, "movement");
        var acciones = (PlayerActions)Campo(hud, "actions");
        TouchControlRegion izquierda = hud.transform.Find("Touch Canvas/Safe Area/Move Left").GetComponent<TouchControlRegion>();
        TouchControlRegion derecha = hud.transform.Find("Touch Canvas/Safe Area/Move Right").GetComponent<TouchControlRegion>();
        TouchControlRegion apuntado = hud.transform.Find("Touch Canvas/Safe Area/Aim and Fire").GetComponent<TouchControlRegion>();
        var flujo = UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
        try
        {
            Canvas.ForceUpdateCanvases();
            foreach (var region in hud.GetComponentsInChildren<TouchControlRegion>()) ComprobarLimites(region);
            izquierda.OnPointerDown(Puntero(11));
            Exigir((float)Campo(movimiento, "touchHorizontal") == -1f, "Primer dedo mueve a la izquierda.");
            izquierda.OnPointerUp(Puntero(99));
            Exigir((float)Campo(movimiento, "touchHorizontal") == -1f, "Otro dedo no libera el control.");
            derecha.OnPointerDown(Puntero(22));
            Exigir((float)Campo(movimiento, "touchHorizontal") == 0f, "Dos flechas opuestas se compensan.");
            derecha.OnPointerUp(Puntero(22));
            Exigir((float)Campo(movimiento, "touchHorizontal") == -1f, "Soltar un dedo conserva el otro.");
            izquierda.OnPointerExit(Puntero(11));
            Exigir((float)Campo(movimiento, "touchHorizontal") == 0f, "Salir de una flecha libera movimiento.");

            var punto = RectTransformUtility.WorldToScreenPoint(null, apuntado.transform.position + Vector3.right * 40f);
            apuntado.OnPointerDown(Puntero(33, punto));
            Exigir((bool)Campo(acciones, "touchFireHeld"), "Apuntado mantiene disparo.");
            apuntado.OnPointerExit(Puntero(33));
            Exigir((bool)Campo(acciones, "touchFireHeld"), "Arrastre fuera conserva apuntado.");
            apuntado.OnPointerUp(Puntero(44));
            Exigir((bool)Campo(acciones, "touchFireHeld"), "Otro dedo no interrumpe disparo.");
            izquierda.OnPointerDown(Puntero(11));
            flujo.PauseGame();
            Exigir(!(bool)Campo(acciones, "touchFireHeld") &&
                (float)Campo(movimiento, "touchHorizontal") == 0f, "Pausa cancela ambos dedos.");
            apuntado.OnPointerDown(Puntero(55, punto));
            Exigir(!(bool)Campo(acciones, "touchFireHeld"), "Pausa rechaza nuevas entradas.");
            flujo.ResumeGame();
            izquierda.OnPointerDown(Puntero(11));
            typeof(MobileControlsHUD).GetMethod("OnApplicationPause", Privado).Invoke(hud, new object[] { true });
            Exigir((float)Campo(movimiento, "touchHorizontal") == 0f, "Segundo plano libera toques.");

            Exigir(ConfiguracionMovil.CalcularCadencia(60) == 60 &&
                ConfiguracionMovil.CalcularCadencia(90) == 45 &&
                ConfiguracionMovil.CalcularCadencia(120) == 60 &&
                ConfiguracionMovil.CalcularCadencia(0) == 60, "Cadencia compatible con 60/90/120 Hz.");
            string resultado = "CONTROLES_MOVILES_OK: multitáctil, salida, apuntado, pausa, segundo plano y límites " + Screen.width + "x" + Screen.height;
            Debug.Log(resultado);
            return resultado;
        }
        finally
        {
            flujo.ResumeGame();
            MobileControlsHUD.CancelActiveTouches();
            typeof(MobileControlsHUD).GetMethod("SetVisible", Privado).Invoke(hud, new object[] { visibleAnterior });
        }
    }

    private static PointerEventData Puntero(int id, Vector2 posicion = default) =>
        new PointerEventData(EventSystem.current) { pointerId = id, position = posicion };

    private static object Campo(object objeto, string nombre) => objeto.GetType().GetField(nombre, Privado).GetValue(objeto);

    private static void ComprobarLimites(TouchControlRegion region)
    {
        var esquinas = new Vector3[4];
        ((RectTransform)region.transform).GetWorldCorners(esquinas);
        Rect segura = Screen.safeArea;
        foreach (Vector3 esquina in esquinas)
        {
            Vector2 punto = RectTransformUtility.WorldToScreenPoint(null, esquina);
            Exigir(punto.x >= segura.xMin - 1f && punto.x <= segura.xMax + 1f &&
                punto.y >= segura.yMin - 1f && punto.y <= segura.yMax + 1f,
                "Control dentro del área segura: " + region.name);
        }
        Exigir(esquinas[2].x - esquinas[0].x >= 44f && esquinas[2].y - esquinas[0].y >= 44f,
            "Superficie táctil suficiente: " + region.name);
    }

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion) throw new InvalidOperationException("Comprobación móvil: " + mensaje);
    }
}
