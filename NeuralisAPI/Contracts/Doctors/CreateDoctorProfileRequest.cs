namespace NeuralisAPI.Contracts.Doctors
{
    public class CreateDoctorProfileRequest
    {
        public string Specialization { get; set; } = null!;
        public string LicenseNumber { get; set; } = null!;
    }
}
