using MedicalFeaturePrototype.Api.Models;

namespace MedicalFeaturePrototype.Api.Services.Interfaces;

public interface IPatientTextService
{
    string BuildFullText(PatientRecord patient);
}
