using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Buffers compartidos que crecen sin descartar impactos en escenas concurridas.</summary>
internal sealed class ConsultasFisicasCombate : IDisposable
{
    private static readonly Stack<ConsultasFisicasCombate> disponibles = new();
    private static readonly ComparadorImpactos comparador = new();
    private bool devuelta;

    internal readonly List<RaycastHit2D> Impactos = new(16);
    internal readonly List<Collider2D> Solapamientos = new(16);
    internal readonly HashSet<EnemyHealth> EnemigosDanados = new();

    internal static ConsultasFisicasCombate Obtener()
    {
        var consulta = disponibles.Count > 0 ? disponibles.Pop() : new ConsultasFisicasCombate();
        consulta.devuelta = false;
        return consulta;
    }

    internal void Barrer(Vector2 origen, float radio, Vector2 direccion, float distancia)
    {
        Physics2D.CircleCast(origen, radio, direccion, CrearFiltro(Physics2D.DefaultRaycastLayers),
            Impactos, distancia);
    }

    internal void TrazarLinea(Vector2 origen, Vector2 destino)
    {
        Physics2D.Linecast(origen, destino, CrearFiltro(Physics2D.DefaultRaycastLayers), Impactos);
    }

    internal void Solapar(Vector2 origen, float radio)
    {
        // OverlapCircleAll incluye Ignore Raycast; los barridos y líneas no lo incluyen.
        Physics2D.OverlapCircle(origen, radio, CrearFiltro(Physics2D.AllLayers), Solapamientos);
    }

    internal void OrdenarPenetracion() => Impactos.Sort(comparador);

    private static ContactFilter2D CrearFiltro(int capas)
    {
        var filtro = ContactFilter2D.noFilter;
        filtro.SetLayerMask(capas);
        filtro.useTriggers = Physics2D.queriesHitTriggers;
        return filtro;
    }

    public void Dispose()
    {
        if (devuelta) return;
        devuelta = true;
        Impactos.Clear();
        Solapamientos.Clear();
        EnemigosDanados.Clear();
        // Cada llamada alquila su propio buffer, incluso si un evento provoca otra explosión.
        if (disponibles.Count < 8) disponibles.Push(this);
    }

    private sealed class ComparadorImpactos : IComparer<RaycastHit2D>
    {
        public int Compare(RaycastHit2D primero, RaycastHit2D segundo)
        {
            int orden = primero.distance.CompareTo(segundo.distance);
            if (orden != 0) return orden;
            bool muroPrimero = EsMuro(primero.collider);
            bool muroSegundo = EsMuro(segundo.collider);
            return muroSegundo.CompareTo(muroPrimero);
        }

        private static bool EsMuro(Collider2D collider) => collider != null && !collider.isTrigger &&
            collider.GetComponentInParent<EnemyHealth>() == null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar() => disponibles.Clear();
}
