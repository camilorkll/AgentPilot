namespace AgentPilot.Domain.Conversations;

/// <summary>Valoración de una respuesta: pulgar arriba o abajo.</summary>
public enum FeedbackRating { Positive, Negative }

/// <summary>
/// Valoración que un agente da a una respuesta del asistente (un mensaje).
/// Alimenta el dashboard de calidad y el dataset de evaluación.
///
/// Hay como mucho UNA por mensaje, y se puede rectificar: el porcentaje de
/// respuestas útiles es positivos entre valoradas, así que dos filas para el mismo
/// mensaje contarían esa respuesta dos veces y falsearían la métrica. La unicidad
/// la garantiza además un índice en la base de datos, no solo esta clase.
/// </summary>
public class Feedback
{
    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public FeedbackRating Rating { get; private set; }
    public string? Comment { get; private set; }

    /// <summary>Quién valoró por última vez (una rectificación sustituye al anterior).</summary>
    public string? CreatedBy { get; private set; }

    /// <summary>Cuándo se valoró por última vez.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private Feedback() { } // EF

    public Feedback(Guid messageId, FeedbackRating rating, string? comment, string? createdBy)
    {
        Id = Guid.NewGuid();
        MessageId = messageId;
        Rating = rating;
        Comment = Limpiar(comment);
        CreatedBy = createdBy;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Rectifica la valoración. Cambiar de pulgar borra el comentario anterior si no
    /// se aporta uno nuevo: un motivo escrito para un «no útil» deja de tener sentido
    /// en cuanto la respuesta pasa a considerarse útil.
    /// </summary>
    public void Actualizar(FeedbackRating rating, string? comment, string? createdBy)
    {
        Rating = rating;
        Comment = Limpiar(comment);
        CreatedBy = createdBy;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private static string? Limpiar(string? comment)
    {
        var limpio = comment?.Trim();
        return string.IsNullOrEmpty(limpio) ? null : limpio;
    }
}
