namespace NeuralisAPI.Contracts.Doctors
{
    public class UpdateDoctorProfileRequest
    {
        public string Specialization { get; set; } = null!;
        public string LicenseNumber { get; set; } = null!;
    }
}
