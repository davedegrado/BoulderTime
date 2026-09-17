using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Gyms;
using BoulderTime.Application.Images;
using BoulderTime.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class ImagesController(ImageService images) : ControllerBase
{
    [HttpPost("api/users/me/avatar-uploads")]
    public Task<UploadTicket> AvatarUpload([FromBody] ImageUploadRequest request, CancellationToken ct) => images.CreateAvatarUploadAsync(request, ct);

    [HttpPut("api/users/me/avatar")]
    public Task<CurrentUserDto> SetAvatar([FromBody] SetImageRequest request, CancellationToken ct) => images.SetAvatarAsync(request, ct);

    [HttpPost("api/gyms/{gymId:guid}/image-uploads")]
    public Task<UploadTicket> GymImageUpload(Guid gymId, [FromBody] GymImageUploadRequest request, CancellationToken ct) => images.CreateGymImageUploadAsync(gymId, request, ct);

    [HttpPut("api/gyms/{gymId:guid}/logo")]
    public Task<GymDetailDto> SetLogo(Guid gymId, [FromBody] SetImageRequest request, CancellationToken ct) => images.SetGymImageAsync(gymId, GymImageKind.Logo, request, ct);

    [HttpPut("api/gyms/{gymId:guid}/cover")]
    public Task<GymDetailDto> SetCover(Guid gymId, [FromBody] SetImageRequest request, CancellationToken ct) => images.SetGymImageAsync(gymId, GymImageKind.Cover, request, ct);
}
