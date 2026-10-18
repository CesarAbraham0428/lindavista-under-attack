using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Regresión de multitáctil, cancelación y tamaño de los controles reales.</summary>
public static class ComprobacionesMoviles
{
    private const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Lindavista/Comprobar controles móviles (Play Mode)")]
    public static void Comprobar() => Ejecutar();

    public static string Ejecutar()
    {
        Exigir(EditorApplication.isPlaying && !GameFlowController.IsPaused &&
            !GameFlowController.IsMatchEnded && !PistolUpgradeShop.IsOpen && !BossIntroduction.IsActive,
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
            var clicDerecho = Puntero(77);
            clicDerecho.button = PointerEventData.InputButton.Right;
            izquierda.OnPointerDown(clicDerecho);
            Exigir((float)Campo(movimiento, "touchHorizontal") == 0f, "Un clic secundario no captura el control táctil.");
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
            typeof(PlayerActions).GetField("fireRequested", Privado).SetValue(acciones, true);
            apuntado.OnPointerUp(Puntero(33));
            Exigir(!(bool)Campo(acciones, "touchFireHeld") && !(bool)Campo(acciones, "fireRequested"),
                "Soltar cancela también el disparo pendiente de LateUpdate.");
            apuntado.OnPointerDown(Puntero(33, punto));
            izquierda.OnPointerDown(Puntero(11));
            acciones.SetInputLocked(true);
            Exigir(!(bool)Campo(acciones, "touchFireHeld") && (float)Campo(movimiento, "touchHorizontal") == 0f,
                "Bloquear acciones libera inmediatamente ambos dedos.");
            izquierda.OnPointerDown(Puntero(88));
            Exigir((float)Campo(movimiento, "touchHorizontal") == 0f, "Un bloqueo rechaza dedos nuevos.");
            acciones.SetInputLocked(false);
            Exigir(!(bool)Campo(acciones, "touchFireHeld") && (float)Campo(movimiento, "touchHorizontal") == 0f,
                "Desbloquear no recupera entradas antiguas.");
            apuntado.OnPointerDown(Puntero(33, punto));
            apuntado.OnCancel(new BaseEventData(EventSystem.current));
            Exigir(!(bool)Campo(acciones, "touchFireHeld"), "El evento Cancel libera el apuntado.");
            apuntado.OnPointerDown(Puntero(33, punto));
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

            ComprobarIntroduccion(acciones, hud);
            ComprobarSeparacionJefe(hud);

            Exigir(ConfiguracionMovil.CalcularCadencia(60) == 60 &&
                ConfiguracionMovil.CalcularCadencia(90) == 45 &&
                ConfiguracionMovil.CalcularCadencia(120) == 60 &&
                ConfiguracionMovil.CalcularCadencia(0) == 60, "Cadencia compatible con 60/90/120 Hz.");
            string resultado = "CONTROLES_MOVILES_OK: multitáctil, cancelación, bloqueo, introducción, pausa, segundo plano y límites " + Screen.width + "x" + Screen.height;
            Debug.Log(resultado);
            return resultado;
        }
        finally
        {
            flujo.ResumeGame();
            acciones.SetInputLocked(false);
            MobileControlsHUD.CancelActiveTouches();
            typeof(MobileControlsHUD).GetMethod("SetVisible", Privado).Invoke(hud, new object[] { visibleAnterior });
        }
    }

    private static PointerEventData Puntero(int id, Vector2 posicion = default) =>
        new PointerEventData(EventSystem.current) { pointerId = id, position = posicion };

    private static object Campo(object objeto, string nombre) => objeto.GetType().GetField(nombre, Privado).GetValue(objeto);

    private static void ComprobarIntroduccion(PlayerActions acciones, MobileControlsHUD hud)
    {
        var go = new GameObject("Mobile Introduction Verification");
        var intro = go.AddComponent<BossIntroduction>();
        var activo = typeof(BossIntroduction).GetField("active", BindingFlags.Static | BindingFlags.NonPublic);
        Touchscreen anterior = Touchscreen.current;
        Touchscreen prueba = null;
        try
        {
            activo.SetValue(null, intro);
            typeof(BossIntroduction).GetField("presenting", Privado).SetValue(intro, true);
            typeof(BossIntroduction).GetField("<LineIndex>k__BackingField", Privado).SetValue(intro, BossIntroduction.Lines.Length - 1);
            Exigir(!acciones.CanReceiveCombatInput && !hud.PuedeRecibirEntrada,
                "La introducción bloquea combate y movimiento incluso sin una cinemática externa.");
            typeof(BossIntroduction).GetMethod("OnApplicationPause", Privado).Invoke(intro, new object[] { true });
            intro.Advance();
            Exigir(!intro.IsComplete, "Suspender no avanza ni termina el diálogo.");
            typeof(BossIntroduction).GetMethod("OnApplicationPause", Privado).Invoke(intro, new object[] { false });
            typeof(BossIntroduction).GetMethod("OnApplicationFocus", Privado).Invoke(intro, new object[] { false });
            intro.Advance();
            Exigir(!intro.IsComplete, "La pérdida de foco bloquea también Advance directo.");
            typeof(BossIntroduction).GetMethod("OnApplicationFocus", Privado).Invoke(intro, new object[] { true });
            intro.Advance();
            Exigir(intro.IsComplete, "El diálogo vuelve a aceptar entrada al recuperar foco.");
            prueba = InputSystem.AddDevice<Touchscreen>("Cinematic Multi Touch Verification");
            // Editor commands can run against Editor buffers while the default
            // InputState.Change target is Dynamic. Write into the queried buffers.
            InputUpdateType actualizacion = InputState.currentUpdateType;
            InputState.Change(prueba.primaryTouch, default(TouchState), actualizacion);
            InputState.Change(prueba.touches[1], new TouchState
            {
                touchId = 2,
                phase = UnityEngine.InputSystem.TouchPhase.Stationary,
                position = new Vector2(100f, 100f)
            }, actualizacion);
            prueba.MakeCurrent();
            Exigir(!prueba.primaryTouch.press.isPressed && prueba.touches[1].press.isPressed,
                "El dispositivo temporal conserva solo un dedo secundario.");
            Exigir((bool)typeof(BossIntroduction).GetMethod("PointerHeld", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null),
                "El cierre de diálogo espera el dedo secundario aunque primaryTouch ya esté libre.");
        }
        finally
        {
            if (prueba != null) InputSystem.RemoveDevice(prueba);
            if (anterior != null && anterior.added) anterior.MakeCurrent();
            activo.SetValue(null, null);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void ComprobarSeparacionJefe(MobileControlsHUD hud)
    {
        var jefe = UnityEngine.Object.FindFirstObjectByType<BossHealthHUD>();
        if (jefe == null) return;
        typeof(BossHealthHUD).GetMethod("UpdateLayout", Privado).Invoke(jefe, null);
        Canvas.ForceUpdateCanvases();
        var panel = (RectTransform)Campo(jefe, "panel");
        Rect limitesJefe = LimitesPantalla(panel);
        foreach (var region in hud.GetComponentsInChildren<TouchControlRegion>())
            Exigir(!limitesJefe.Overlaps(LimitesPantalla((RectTransform)region.transform)),
                "Vida y aviso del jefe separados del área táctil: " + region.name);
    }

    private static Rect LimitesPantalla(RectTransform rect)
    {
        var esquinas = new Vector3[4];
        rect.GetWorldCorners(esquinas);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, esquinas[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(null, esquinas[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

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
