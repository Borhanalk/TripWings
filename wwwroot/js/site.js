document.addEventListener('DOMContentLoaded', function() {
    const sidebarToggle = document.getElementById('sidebarToggle');
    const sidebar = document.getElementById('sidebar');
    const sidebarCollapse = document.getElementById('sidebarCollapse');
    
    if (sidebarToggle) {
        sidebarToggle.addEventListener('click', function(e) {
            e.stopPropagation();
            sidebar.classList.toggle('active');
        });
    }
    
    if (sidebarCollapse) {
        sidebarCollapse.addEventListener('click', function(e) {
            e.stopPropagation();
            sidebar.classList.toggle('active');
        });
    }
    
    document.addEventListener('click', function(event) {
        if (window.innerWidth < 992) {
            const isClickInsideSidebar = sidebar && sidebar.contains(event.target);
            const isClickOnToggle = sidebarToggle && sidebarToggle.contains(event.target);
            
            if (!isClickInsideSidebar && !isClickOnToggle && sidebar && sidebar.classList.contains('active')) {
                sidebar.classList.remove('active');
            }
        }
    });
    
    const currentPath = window.location.pathname;
    const navLinks = document.querySelectorAll('.sidebar .nav-link');
    navLinks.forEach(link => {
        if (link.getAttribute('href') === currentPath || currentPath.startsWith(link.getAttribute('href'))) {
            link.classList.add('active');
        }
    });
});
