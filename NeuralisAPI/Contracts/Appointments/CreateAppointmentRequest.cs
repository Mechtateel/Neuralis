namespace NeuralisAPI.Contracts.Appointments
{
    public class CreateAppointmentRequest
    {
        public Guid DoctorProfileId { get; set; }

        public DateTime ScheduledAtUtc { get; set; }
    }
}
