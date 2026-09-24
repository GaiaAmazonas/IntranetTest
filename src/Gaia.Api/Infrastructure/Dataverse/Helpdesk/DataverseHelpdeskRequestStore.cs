using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gaia.Modules.Helpdesk;

namespace Gaia.Api.Infrastructure.Dataverse.Helpdesk;

internal sealed class DataverseHelpdeskRequestStore(
    IDataverseDelegatedClientFactory clients,
    DataverseHelpdeskWorkflowExecutionWriter workflow) : IHelpdeskRequestStore
{
    public async Task<CreatedHelpdeskRequest> CreateAsync(CreateHelpdeskRequest request, Guid requesterId,
        DateTimeOffset now, CancellationToken token)
    {
        var client = await clients.CreateAsync();
        var requestTable = await DataverseMetadataResolver.TableAsync(client, "gaia_solicitud", token);
        var serviceTable = await DataverseMetadataResolver.TableAsync(client, "gaia_servicio", token);
        var stateTable = await DataverseMetadataResolver.TableAsync(client, "gaia_estadosolicitud", token);
        var thirdPartyTable = await DataverseMetadataResolver.TableAsync(client, "gaia_terceros", token);
        var organizationTable = await DataverseMetadataResolver.TableAsync(client, "gaia_organizacion", token);
        var formTable = await DataverseMetadataResolver.TableAsync(client, "gaia_formularioservicio", token);

        var serviceFields = ServiceFields.From(serviceTable);
        var service = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{serviceTable.EntitySetName}({request.ServiceId:D})?$select=statecode,{serviceFields.Visible},{serviceFields.Days},_{serviceFields.Unit}_value,_{serviceFields.Manager}_value,_{serviceFields.Form}_value,_{serviceFields.Flow}_value", token);
        if (service is null || Int(service.Value, "statecode") != 0 || !Bool(service.Value, serviceFields.Visible))
            throw new ArgumentException("El servicio seleccionado no existe o no está disponible.");

        var stateCodeField = stateTable.Attribute("gaia_Codigo");
        var states = await DataverseJson.ReadAllAsync(client,
            $"{stateTable.EntitySetName}?$select={stateTable.PrimaryIdAttribute},{stateTable.PrimaryNameAttribute},{stateCodeField}&$filter=statecode eq 0", token);
        var submittedStates = states.Where(row =>
            Normalize(Text(row, stateCodeField)) == "RADICADA" ||
            Normalize(Text(row, stateTable.PrimaryNameAttribute)) == "RADICADA").ToArray();
        if (submittedStates.Length != 1)
            throw new InvalidOperationException("Debe existir exactamente un estado activo con código o nombre RADICADA para Helpdesk.");
        var submittedStateId = GuidValue(submittedStates[0], stateTable.PrimaryIdAttribute);
        var requesterExists = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{thirdPartyTable.EntitySetName}({requesterId:D})?$select={thirdPartyTable.PrimaryIdAttribute},statecode", token);
        if (requesterExists is null || Int(requesterExists.Value, "statecode") != 0)
            throw new UnauthorizedAccessException("El usuario no está asociado con un tercero activo.");

        var days = Math.Max(1, Int(service.Value, serviceFields.Days));
        var dueDate = await CalculateDueDate(client, now.Date, days, token);
        var requestService = requestTable.Relationship("gaia_Servicio", "gaia_servicio");
        var requestRequester = requestTable.Relationship("gaia_Solicitante", "gaia_terceros");
        var requestState = requestTable.Relationship("gaia_EstadoActual", "gaia_estadosolicitud");
        var payload = new Dictionary<string, object?>
        {
            [requestTable.Attribute("gaia_Asunto")] = request.Subject,
            [requestTable.Attribute("gaia_DescripcionInicial")] = request.Description,
            [requestTable.Attribute("gaia_FechaRadicacion")] = now,
            [requestTable.Attribute("gaia_FechaInicioSLA")] = now,
            [requestTable.Attribute("gaia_DiasObjetivo")] = requestTable.EncodedIntegerValue("gaia_DiasObjetivo", days),
            [requestTable.Attribute("gaia_FechaLimiteOriginal")] = dueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            [requestTable.Attribute("gaia_FechaLimiteActual")] = dueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            [requestTable.Attribute("gaia_DiasHabilesPausados")] = requestTable.EncodedIntegerValue("gaia_DiasHabilesPausados", 0),
            [requestService.NavigationProperty + "@odata.bind"] = $"/{serviceTable.EntitySetName}({request.ServiceId:D})",
            [requestRequester.NavigationProperty + "@odata.bind"] = $"/{thirdPartyTable.EntitySetName}({requesterId:D})",
            [requestState.NavigationProperty + "@odata.bind"] = $"/{stateTable.EntitySetName}({submittedStateId:D})",
            ["statecode"] = 0
        };
        BindOptional(payload, requestTable, "gaia_UnidadResponsable", organizationTable, service.Value, serviceFields.Unit);
        BindOptional(payload, requestTable, "gaia_ResponsableInterno", thirdPartyTable, service.Value, serviceFields.Manager);
        BindOptional(payload, requestTable, "gaia_FormularioUtilizado", formTable, service.Value, serviceFields.Form);

        Guid createdId;
        using (var response = await client.PostAsJsonAsync(requestTable.EntitySetName, payload, token))
        {
            await Ensure(response, token);
            createdId = CreatedId(response);
        }
        var createdAnswers=new List<(DataverseTableMetadata Table,Guid Id)>();
        try
        {
            await PersistAnswers(client,requestTable,createdId,request.Answers??[],createdAnswers,token);
            await AppendHistory(client, createdId, requesterId, now, 299540102, "Solicitud radicada", token);
            var flowId=OptionalGuid(service.Value,$"_{serviceFields.Flow}_value");
            if(flowId.HasValue)await workflow.StartAsync(createdId,flowId.Value,requesterId,now,token);
        }
        catch
        {
            foreach(var answer in createdAnswers.AsEnumerable().Reverse())
            {
                using var answerDeactivate=new HttpRequestMessage(HttpMethod.Patch,$"{answer.Table.EntitySetName}({answer.Id:D})"){Content=JsonContent.Create(new Dictionary<string,object?>{{"statecode",1}})};
                answerDeactivate.Headers.TryAddWithoutValidation("If-Match","*");using var ignoredAnswer=await client.SendAsync(answerDeactivate,CancellationToken.None);
            }
            using var deactivate = new HttpRequestMessage(HttpMethod.Patch, $"{requestTable.EntitySetName}({createdId:D})")
                { Content = JsonContent.Create(new Dictionary<string, object?> { ["statecode"] = 1 }) };
            deactivate.Headers.TryAddWithoutValidation("If-Match", "*");
            using var ignored = await client.SendAsync(deactivate, CancellationToken.None);
            throw;
        }
        var created = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{requestTable.EntitySetName}({createdId:D})?$select={requestTable.PrimaryNameAttribute}", token)
            ?? throw new InvalidOperationException("No fue posible confirmar la solicitud creada.");
        return new(createdId, Text(created, requestTable.PrimaryNameAttribute) ?? createdId.ToString("D"), now, dueDate);
    }

    private static async Task PersistAnswers(HttpClient client,DataverseTableMetadata requestTable,Guid requestId,
        IReadOnlyList<HelpdeskFieldAnswer> answers,List<(DataverseTableMetadata Table,Guid Id)> created,CancellationToken token)
    {
        if(answers.Count==0)return;var fieldTable=await DataverseMetadataResolver.TableAsync(client,"gaia_campoformulario",token);var responseTable=await DataverseMetadataResolver.TableAsync(client,"gaia_respuestacampo",token);var optionTable=await DataverseMetadataResolver.TableAsync(client,"gaia_opcioncampoformulario",token);var junctionTable=await DataverseMetadataResolver.TableAsync(client,"gaia_respuestaopcioncampo",token);var dataType=fieldTable.Attribute("gaia_TipoDato");
        foreach(var answer in answers)
        {
            var field=await DataverseMetadataResolver.ReadOneAsync(client,$"{fieldTable.EntitySetName}({answer.FieldId:D})?$select={fieldTable.PrimaryNameAttribute},{dataType},statecode",token)??throw new ArgumentException("El formulario contiene un campo inexistente.");if(Int(field,"statecode")!=0)throw new ArgumentException("El formulario contiene un campo inactivo.");var value=answer.Value?.Trim();var payload=new Dictionary<string,object?>{{responseTable.PrimaryNameAttribute,$"{Text(field,fieldTable.PrimaryNameAttribute)??"Respuesta"} - {requestId:D}"},{responseTable.Relationship("gaia_Solicitud","gaia_solicitud").NavigationProperty+"@odata.bind",$"/{requestTable.EntitySetName}({requestId:D})"},{responseTable.Relationship("gaia_CampoFormulario","gaia_campoformulario").NavigationProperty+"@odata.bind",$"/{fieldTable.EntitySetName}({answer.FieldId:D})"},{"statecode",0}};
            switch(Int(field,dataType)){case 299540041:if(int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var integer))payload[responseTable.Attribute("gaia_ValorEntero")]=integer;break;case 299540042:if(decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var number))payload[responseTable.Attribute("gaia_ValorDecimal")]=number;break;case 299540043:payload[responseTable.Attribute("gaia_ValorFecha")]=value;break;case 299540044:payload[responseTable.Attribute("gaia_ValorFechaHora")]=value;break;case 299540045:if(bool.TryParse(value,out var boolean))payload[responseTable.Attribute("gaia_ValorBooleano")]=boolean;break;default:if(!string.IsNullOrWhiteSpace(value))payload[responseTable.Attribute("gaia_ValorTexto")]=value;break;}
            using var response=await client.PostAsJsonAsync(responseTable.EntitySetName,payload,token);await Ensure(response,token);var responseId=CreatedId(response);created.Add((responseTable,responseId));
            var optionField=optionTable.Attribute("gaia_CampoFormulario");
            foreach(var optionId in answer.OptionIds??[]){var option=await DataverseMetadataResolver.ReadOneAsync(client,$"{optionTable.EntitySetName}({optionId:D})?$select={optionTable.PrimaryIdAttribute},statecode,_{optionField}_value",token);if(option is null||Int(option.Value,"statecode")!=0||OptionalGuid(option.Value,$"_{optionField}_value")!=answer.FieldId)throw new ArgumentException("La respuesta contiene una opción que no pertenece al campo.");var junction=new Dictionary<string,object?>{{junctionTable.PrimaryNameAttribute,$"Opción {responseId:D}"},{junctionTable.Relationship("gaia_RespuestaCampo","gaia_respuestacampo").NavigationProperty+"@odata.bind",$"/{responseTable.EntitySetName}({responseId:D})"},{junctionTable.Relationship("gaia_OpcionCampoFormulario","gaia_opcioncampoformulario").NavigationProperty+"@odata.bind",$"/{optionTable.EntitySetName}({optionId:D})"},{"statecode",0}};using var junctionResponse=await client.PostAsJsonAsync(junctionTable.EntitySetName,junction,token);await Ensure(junctionResponse,token);created.Add((junctionTable,CreatedId(junctionResponse)));}
        }
    }

    private static async Task<DateOnly> CalculateDueDate(HttpClient client, DateTimeOffset start, int businessDays, CancellationToken token)
    {
        var holidayTable = await DataverseMetadataResolver.TableAsync(client, "gaia_dianolaborable", token);
        var dateField = holidayTable.Attribute("gaia_Fecha");
        var until = DateOnly.FromDateTime(start.UtcDateTime).AddDays(businessDays * 3 + 14);
        var holidays = (await DataverseJson.ReadAllAsync(client,
            $"{holidayTable.EntitySetName}?$select={dateField}&$filter=statecode eq 0 and {dateField} le {until:yyyy-MM-dd}", token))
            .Select(row => DateOnly.TryParse(Text(row, dateField), out var value) ? value : (DateOnly?)null)
            .Where(value => value.HasValue).Select(value => value!.Value).ToHashSet();
        var current = DateOnly.FromDateTime(start.UtcDateTime); var remaining = businessDays;
        while (remaining > 0)
        {
            current = current.AddDays(1);
            if (current.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || holidays.Contains(current)) continue;
            remaining--;
        }
        return current;
    }

    private static void BindOptional(Dictionary<string, object?> payload, DataverseTableMetadata requestTable,
        string requestSchema, DataverseTableMetadata target, JsonElement service, string serviceLookup)
    {
        var id = OptionalGuid(service, $"_{serviceLookup}_value"); if (!id.HasValue) return;
        var relationship = requestTable.Relationship(requestSchema, target.LogicalName);
        payload[relationship.NavigationProperty + "@odata.bind"] = $"/{target.EntitySetName}({id:D})";
    }

    private static async Task AppendHistory(HttpClient client, Guid requestId, Guid actorId, DateTimeOffset now,
        int movement, string name, CancellationToken token)
    {
        var history = await DataverseMetadataResolver.TableAsync(client, "gaia_historialsolicitud", token);
        var request = await DataverseMetadataResolver.TableAsync(client, "gaia_solicitud", token);
        var actor = await DataverseMetadataResolver.TableAsync(client, "gaia_terceros", token);
        var payload = new Dictionary<string, object?>
        {
            [history.PrimaryNameAttribute] = name,
            [history.Attribute("gaia_OperacionId")] = Guid.NewGuid().ToString("D"),
            [history.Attribute("gaia_TipoMovimiento")] = history.EncodedIntegerValue("gaia_TipoMovimiento", movement),
            [history.Attribute("gaia_Origen")] = history.EncodedIntegerValue("gaia_Origen", 299540120),
            [history.Attribute("gaia_VisibleAlSolicitante")] = true,
            [history.Attribute("gaia_FechaEvento")] = now,
            [history.Relationship("gaia_Solicitud", "gaia_solicitud").NavigationProperty + "@odata.bind"] = $"/{request.EntitySetName}({requestId:D})",
            [history.Relationship("gaia_Actor", "gaia_terceros").NavigationProperty + "@odata.bind"] = $"/{actor.EntitySetName}({actorId:D})",
            ["statecode"] = 0
        };
        using var response = await client.PostAsJsonAsync(history.EntitySetName, payload, token);
        await Ensure(response, token);
    }

    private static async Task Ensure(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        var content = await response.Content.ReadAsStringAsync(token);
        throw new InvalidOperationException($"Dataverse rechazó la radicación ({(int)response.StatusCode}): {Error(content)}");
    }
    private static string Error(string content) { try { using var json=JsonDocument.Parse(content); return json.RootElement.GetProperty("error").GetProperty("message").GetString()??"Error sin descripción."; } catch(JsonException) { return "Respuesta no válida de Dataverse."; } }
    private static Guid CreatedId(HttpResponseMessage response) { var uri=response.Headers.TryGetValues("OData-EntityId",out var values)?values.SingleOrDefault():null;var match=Regex.Match(uri??"",@"\(([0-9a-f-]{36})\)$");return match.Success?Guid.Parse(match.Groups[1].Value):throw new InvalidOperationException("Dataverse no devolvió el identificador de la solicitud."); }
    private static string? Text(JsonElement row,string name)=>row.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
    private static string Normalize(string? value)=>string.Concat((value??string.Empty).Normalize(NormalizationForm.FormD).Where(character=>CharUnicodeInfo.GetUnicodeCategory(character)!=UnicodeCategory.NonSpacingMark)).ToUpperInvariant().Trim();
    private static int Int(JsonElement row,string name)=>DataverseJson.OptionalInt32(row,name)??0;
    private static bool Bool(JsonElement row,string name)=>row.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.True;
    private static Guid GuidValue(JsonElement row,string name)=>OptionalGuid(row,name)??throw new InvalidOperationException($"Dataverse no devolvió {name}.");
    private static Guid? OptionalGuid(JsonElement row,string name)=>Guid.TryParse(Text(row,name),out var id)?id:null;
    private sealed record ServiceFields(string Visible,string Days,string Unit,string Manager,string Form,string Flow)
    {
        public static ServiceFields From(DataverseTableMetadata table)=>new(table.Attribute("gaia_VisibleAlSolicitante"),table.Attribute("gaia_DiasGestionHabiles"),table.Attribute("gaia_UnidadResponsable"),table.Attribute("gaia_ResponsablePredeterminado"),table.Attribute("gaia_FormularioVigente"),table.Attribute("gaia_FlujoVigente"));
    }
}
