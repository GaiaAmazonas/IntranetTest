using System.Globalization;

namespace Gaia.Modules.Helpdesk;

public sealed record CreateHelpdeskRequest(Guid ServiceId, string Subject, string Description,
    IReadOnlyList<HelpdeskFieldAnswer>? Answers = null);
public sealed record CreatedHelpdeskRequest(Guid Id, string Number, DateTimeOffset SubmittedAt, DateOnly DueDate);

public interface IHelpdeskRequestStore
{
    Task<CreatedHelpdeskRequest> CreateAsync(CreateHelpdeskRequest request, Guid requesterThirdPartyId,
        DateTimeOffset now, CancellationToken cancellationToken);
}

public interface IHelpdeskRequestApplication
{
    Task<CreatedHelpdeskRequest> CreateAsync(CreateHelpdeskRequest request, Guid requesterThirdPartyId,
        CancellationToken cancellationToken);
}

public sealed class HelpdeskRequestApplication(IHelpdeskRequestStore store, IHelpdeskFormReader forms, TimeProvider timeProvider)
    : IHelpdeskRequestApplication
{
    public async Task<CreatedHelpdeskRequest> CreateAsync(CreateHelpdeskRequest request, Guid requesterThirdPartyId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (requesterThirdPartyId == Guid.Empty) throw new UnauthorizedAccessException("El usuario no está asociado con un tercero activo.");
        if (request.ServiceId == Guid.Empty) throw new ArgumentException("Selecciona un servicio válido.");
        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Trim().Length is < 5 or > 200)
            throw new ArgumentException("El asunto debe tener entre 5 y 200 caracteres.");
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length is < 10 or > 4000)
            throw new ArgumentException("La descripción debe tener entre 10 y 4000 caracteres.");
        var form=await forms.ReadForServiceAsync(request.ServiceId,cancellationToken);
        ValidateAnswers(form,request.Answers??[]);
        return await store.CreateAsync(request with { Subject = request.Subject.Trim(), Description = request.Description.Trim() },
            requesterThirdPartyId, timeProvider.GetUtcNow(), cancellationToken);
    }

    private static void ValidateAnswers(HelpdeskServiceForm? form,IReadOnlyList<HelpdeskFieldAnswer> answers)
    {
        if(form is null){if(answers.Count>0)throw new ArgumentException("El servicio no tiene un formulario publicado.");return;}
        if(answers.Select(x=>x.FieldId).Distinct().Count()!=answers.Count)throw new ArgumentException("No repitas respuestas para el mismo campo.");
        foreach(var field in form.Fields)
        {
            var answer=answers.SingleOrDefault(x=>x.FieldId==field.Id);var value=answer?.Value?.Trim();var present=answer is not null&&(!string.IsNullOrWhiteSpace(value)||answer.OptionIds?.Count>0);
            if(field.Required&&!present)throw new ArgumentException($"El campo {field.Label} es obligatorio.");if(answer is null)continue;
            if(field.MaximumLength.HasValue&&value?.Length>field.MaximumLength)throw new ArgumentException($"El campo {field.Label} supera la longitud permitida.");if(field.MinimumLength.HasValue&&!string.IsNullOrEmpty(value)&&value.Length<field.MinimumLength)throw new ArgumentException($"El campo {field.Label} no alcanza la longitud mínima.");
            if(answer.OptionIds?.Any(id=>field.Options.All(option=>option.Id!=id))==true)throw new ArgumentException($"El campo {field.Label} contiene una opción inválida.");if(!field.AllowsMultiple&&answer.OptionIds?.Count>1)throw new ArgumentException($"El campo {field.Label} solo admite una opción.");
            if(string.IsNullOrWhiteSpace(value))continue;
            decimal? numeric=field.DataType switch{299540041 when int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var integer)=>integer,299540042 when decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var number)=>number,_=>null};
            if(field.DataType is 299540041 or 299540042&&numeric is null)throw new ArgumentException($"El campo {field.Label} debe ser numérico.");if(field.MinimumValue.HasValue&&numeric<field.MinimumValue)throw new ArgumentException($"El campo {field.Label} está por debajo del mínimo permitido.");if(field.MaximumValue.HasValue&&numeric>field.MaximumValue)throw new ArgumentException($"El campo {field.Label} supera el máximo permitido.");
            if(field.DataType==299540043&&!DateOnly.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out _))throw new ArgumentException($"El campo {field.Label} debe contener una fecha válida.");if(field.DataType==299540044&&!DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeLocal,out _))throw new ArgumentException($"El campo {field.Label} debe contener una fecha y hora válidas.");if(field.DataType==299540045&&!bool.TryParse(value,out _))throw new ArgumentException($"El campo {field.Label} debe ser Sí o No.");
        }
        if(answers.Any(answer=>form.Fields.All(field=>field.Id!=answer.FieldId)))throw new ArgumentException("El formulario contiene campos desconocidos.");
    }
}
