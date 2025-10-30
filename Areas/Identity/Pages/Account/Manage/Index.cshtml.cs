using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudyRoom.Models;

namespace StudyRoom.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        public IndexModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string? Username { get; set; }
        public string? Email { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Display(Name = "Display name")]
            [StringLength(50)]
            public string? DisplayName { get; set; }

            [Phone]
            [Display(Name = "Phone number")]
            public string? PhoneNumber { get; set; }
        }

        private static string FriendlyFromEmail(string? userNameOrEmail)
        {
            if (string.IsNullOrWhiteSpace(userNameOrEmail)) return "User";
            var at = userNameOrEmail.IndexOf('@');
            if (at > 0) return userNameOrEmail.Substring(0, at);
            return userNameOrEmail;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("Unable to load user.");

            Username = await _userManager.GetUserNameAsync(user);
            Email = await _userManager.GetEmailAsync(user);
            var phone = await _userManager.GetPhoneNumberAsync(user);

            Input = new InputModel
            {
                DisplayName = string.IsNullOrWhiteSpace(user.DisplayName)
                    ? FriendlyFromEmail(user.UserName ?? user.Email)
                    : user.DisplayName,
                PhoneNumber = phone
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("Unable to load user.");

            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            // Save display name
            var newName = Input.DisplayName?.Trim();
            if (newName != user.DisplayName)
            {
                user.DisplayName = string.IsNullOrWhiteSpace(newName) ? null : newName;
                var res = await _userManager.UpdateAsync(user);
                if (!res.Succeeded)
                {
                    foreach (var e in res.Errors) ModelState.AddModelError(string.Empty, e.Description);
                    await OnGetAsync();
                    return Page();
                }
            }

            // Save phone (default Identity behavior)
            var existingPhone = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != existingPhone)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber ?? "");
                if (!setPhoneResult.Succeeded)
                {
                    foreach (var e in setPhoneResult.Errors) ModelState.AddModelError(string.Empty, e.Description);
                    await OnGetAsync();
                    return Page();
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Your profile has been updated";
            return RedirectToPage();
        }
    }
}
