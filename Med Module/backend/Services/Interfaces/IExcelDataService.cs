using MedicalFeaturePrototype.Api.Models;

namespace MedicalFeaturePrototype.Api.Services.Interfaces;

public interface IExcelDataService
{
    IReadOnlyList<PatientRecord> LoadPatients();
    IReadOnlyList<TargetFeature> LoadFeatures(bool includeComplaintsFeatures, bool includeAnamnesisFeatures);
    PatientRecord? GetPatientById(int id);
}
