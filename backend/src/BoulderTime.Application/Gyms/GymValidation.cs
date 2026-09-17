using BoulderTime.Application.Common;
using BoulderTime.Domain.Gyms;

namespace BoulderTime.Application.Gyms;

internal static class GymValidation
{
    public static void ValidateLocation(double? latitude, double? longitude) =>
        new Validator()
            .Check((latitude is null) == (longitude is null), "latitude", "Set both latitude and longitude.")
            .Check(latitude is null or (>= -90 and <= 90), "latitude", "Latitude must be between -90 and 90.")
            .Check(longitude is null or (>= -180 and <= 180), "longitude", "Longitude must be between -180 and 180.")
            .ThrowIfInvalid();

    public static void ValidateProfile(string? name, string? city, string? description, string? address, string? website, string? email, string? phone)
    {
        new Validator()
            .Check(Input.Trimmed(name).Length >= 2, "name", "Use at least 2 characters.")
            .Check(Input.MaxLength(name, Gym.NameMaxLength), "name", $"Keep it to {Gym.NameMaxLength} characters or fewer.")
            .Check(Input.Trimmed(city).Length >= 2, "city", "Enter the city.")
            .Check(Input.MaxLength(city, Gym.CityMaxLength), "city", $"Keep it to {Gym.CityMaxLength} characters or fewer.")
            .Check(Input.MaxLength(description, Gym.DescriptionMaxLength), "description", $"Keep it to {Gym.DescriptionMaxLength} characters or fewer.")
            .Check(Input.MaxLength(address, Gym.AddressMaxLength), "address", $"Keep it to {Gym.AddressMaxLength} characters or fewer.")
            .Check(Input.OptionalUrl(website), "website", "Enter a full web address starting with https://")
            .Check(Input.OptionalEmail(email), "email", "Enter a valid email address.")
            .Check(Input.MaxLength(phone, Gym.PhoneMaxLength), "phone", "Enter a shorter phone number.")
            .ThrowIfInvalid();
    }
}
