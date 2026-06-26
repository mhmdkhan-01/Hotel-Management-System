using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Hotel_Management_System.Models;
using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_System.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Login()
        {
            // If user is already authenticated, redirect them to their respective board right away
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToRoleDashboard();
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: true, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                return RedirectToRoleDashboard();
            }

            ModelState.AddModelError(string.Empty, "Invalid email address or operational password.");
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToRoleDashboard()
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("Dashboard", "Admin");
            if (User.IsInRole("Kitchen"))
                return RedirectToAction("Index", "Kitchen");
            if (User.IsInRole("Waiter"))
                return RedirectToAction("Index", "Waiter");

            return RedirectToAction("Index", "Customer");
        }
    }

    // Direct Login DTO Model for clean credential mapping
    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}