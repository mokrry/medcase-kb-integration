using MedicalFeaturePrototype.Api.Models;
using MedicalFeaturePrototype.Api.Services.Interfaces;

namespace MedicalFeaturePrototype.Api.Services;

public class PatientTextService : IPatientTextService
{
    public string BuildFullText(PatientRecord patient)
    {
        return string.Join(Environment.NewLine + Environment.NewLine, new[]
        {
            $"Жалобы: {patient.Complaints}",
            $"Анамнез заболевания: {patient.Anamnesis}",
            $"Физикальное обследование: {patient.PhysicalExam}"
        });
    }
}
