document.addEventListener("DOMContentLoaded", function () {

    // 1. Sidebar Toggle Logic
    var toggleButton = document.getElementById("menu-toggle");
    var wrapper = document.getElementById("wrapper");

    if (toggleButton) {
        toggleButton.onclick = function (e) {
            e.preventDefault();
            wrapper.classList.toggle("toggled");
        };
    }

    // 2. Auto-Dismiss Alerts (Success/Error messages disappear after 5s)
    var alerts = document.querySelectorAll('.alert-dismissible');
    alerts.forEach(function (alert) {
        setTimeout(function () {
            var bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        }, 5000);
    });

    // 3. Add 'active' class to Sidebar based on URL
    var currentUrl = window.location.pathname;
    var links = document.querySelectorAll('.list-group-item');

    links.forEach(function (link) {
        if (link.getAttribute('href') === currentUrl) {
            link.classList.add('active');
        }
    });
});