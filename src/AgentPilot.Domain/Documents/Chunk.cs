namespace AgentPilot.Domain.Documents;

/// <summary>
/// Un fragmento de texto de un documento, con su vector de embedding.
/// Es la unidad que se indexa y sobre la que se hace la búsqueda por similitud:
/// cuando el agente pregunta algo, comparamos el vector de la pregunta contra
/// el Embedding de cada Chunk y recuperamos los más parecidos.
/// </summary>
public class Chunk
{
    public Guid Id { get; private set; }

    /// <summary>Documento al que pertenece (clave foránea).</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Posición del fragmento dentro del documento (0, 1, 2, ...).</summary>
    public int Ordinal { get; private set; }

    /// <summary>Texto del fragmento. Es lo que se muestra como cita al agente.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// Vector de embedding. En el dominio es un simple float[]: la conversión
    /// al tipo 'vector' de pgvector se hace en la capa de Infraestructura ya que vamos a usar PostgreSQL con pgvector pero un día se podría cambiar
    /// a Quadrant y esto no se vería afectado.
    /// </summary>
    public float[] Embedding { get; private set; } = [];

    // Constructor privado sin parámetros: lo usa EF Core al materializar desde la BD.
    private Chunk() { }

    public Chunk(int ordinal, string content, float[] embedding)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("El contenido del chunk no puede estar vacío.", nameof(content));
        if (embedding.Length == 0)
            throw new ArgumentException("El chunk debe tener un embedding.", nameof(embedding));

        Id = Guid.NewGuid();
        Ordinal = ordinal;
        Content = content;
        Embedding = embedding;
    }
}
