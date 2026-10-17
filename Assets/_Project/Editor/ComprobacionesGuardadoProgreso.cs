using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Verifica la agrupación de munición sin leer ni escribir el perfil del jugador.</summary>
public static class ComprobacionesGuardadoProgreso
{
    [MenuItem("Tools/Lindavista/Verificar guardado agrupado")]
    public static void Verificar() => Ejecutar();

    public static string Ejecutar()
    {
        string carpeta = Path.Combine(Application.temporaryCachePath, "ComprobacionProgreso-" + Guid.NewGuid().ToString("N"));
        string archivo = Path.Combine(carpeta, "perfil.json");
        try
        {
            var repositorio = new ProgressionRepository(archivo, true);
            Exigir(repositorio.TryCredit(100), "saldo inicial confirmado");
            string guardadoInicial = File.ReadAllText(archivo);
            for (int indice = 0; indice < 10; indice++)
                Exigir(repositorio.TryConsumeAmmo(0, false), "consumo aceptado en memoria");
            Exigir(repositorio.Municion(0, false) == 38 && repositorio.Municion(0, true) == 48,
                "reservas independientes actualizadas inmediatamente");
            Exigir(File.ReadAllText(archivo) == guardadoInicial, "diez disparos no escriben diez archivos");
            Exigir(new ProgressionRepository(archivo).Municion(0, false) == 48, "lectura en disco antes de confirmar");
            Exigir(repositorio.GuardarPendientes(), "confirmación del lote");
            Exigir(new ProgressionRepository(archivo).Municion(0, false) == 38, "lote persistente al reiniciar");

            Exigir(repositorio.TryConsumeAmmo(0, true), "consumo previo a compra");
            Exigir(repositorio.TryUpgradeWeapon(0, false), "compra conserva confirmación inmediata");
            var recargado = new ProgressionRepository(archivo);
            Exigir(recargado.Monedas == 75 && recargado.MejoraArma(0, false) == 1 && recargado.Municion(0, true) == 47,
                "compra y consumos previos se confirman juntos");

            // Las consultas del HUD y del disparo deben evitar copias de inventario.
            long antes = GC.GetAllocatedBytesForCurrentThread();
            int suma = 0;
            for (int indice = 0; indice < 1000; indice++)
                suma += repositorio.Monedas + repositorio.MejoraVida(false) + repositorio.MejoraArma(0, false) + repositorio.Municion(0, false);
            long asignados = GC.GetAllocatedBytesForCurrentThread() - antes;
            Exigir(suma > 0 && asignados == 0, "consultas escalares sin asignaciones administradas");

            Exigir(repositorio.TryCompleteLevel(0), "primera victoria confirmada");
            DateTime fecha = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(archivo, fecha);
            Exigir(repositorio.TryCompleteLevel(0) && File.GetLastWriteTimeUtc(archivo) == fecha,
                "repetir una victoria confirmada evita escrituras innecesarias");

            // Un fallo de disco mantiene los consumos pendientes para reintentarlos.
            Exigir(repositorio.TryConsumeAmmo(0), "consumo anterior a fallo de escritura");
            Directory.CreateDirectory(archivo + ".tmp");
            Exigir(!repositorio.GuardarPendientes() && repositorio.Municion(0, false) == 37,
                "fallo de escritura sin perder los consumos en memoria");
            Exigir(!repositorio.TryUpgradeWeapon(0, false) && repositorio.Monedas == 75 && repositorio.MejoraArma(0, false) == 1,
                "compra fallida sin cobrar ni conceder mejora");
            Directory.Delete(archivo + ".tmp");
            Exigir(repositorio.GuardarPendientes() && new ProgressionRepository(archivo).Municion(0, false) == 37,
                "reintento del lote después del fallo");

            // Al recuperarse desde .bak, el principal corrupto nunca debe reemplazarlo.
            string respaldo = File.ReadAllText(archivo + ".bak");
            File.WriteAllText(archivo, "archivo corrupto");
            var recuperado = new ProgressionRepository(archivo, true);
            Exigir(recuperado.CanSave && recuperado.TryConsumeAmmo(0) && recuperado.GuardarPendientes(),
                "confirmación después de recuperar respaldo");
            Exigir(File.ReadAllText(archivo + ".bak") == respaldo, "respaldo válido conservado durante recuperación");

            string invalido = Path.Combine(carpeta, "invalido.json");
            File.WriteAllText(invalido, "archivo corrupto");
            var bloqueado = new ProgressionRepository(invalido, true);
            Exigir(!bloqueado.CanSave && !bloqueado.TryConsumeAmmo(0) && File.ReadAllText(invalido) == "archivo corrupto",
                "perfil irrecuperable conservado sin consumir munición");

            string resultado = "GUARDADO_PROGRESO_OK: lotes, reservas independientes, compras inmediatas, lecturas sin asignaciones y recuperación verificados.";
            Debug.Log(resultado);
            return resultado;
        }
        finally
        {
            if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true);
        }
    }

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion) throw new Exception("Falló la comprobación de progreso: " + mensaje);
    }
}
