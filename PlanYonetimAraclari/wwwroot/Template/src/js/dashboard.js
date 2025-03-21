/**
 * Dashboard JavaScript işlevselliği
 */

document.addEventListener('DOMContentLoaded', function() {
    initSidebar();
    initUserMenu();
    initResponsive();
    initProjectsDropdown();
    initProjectCreation();
    initQuickNotes();
});

/**
 * Sidebar işlevselliği
 */
function initSidebar() {
    const sidebarToggle = document.getElementById('sidebar-toggle');
    const body = document.body;
    
    sidebarToggle.addEventListener('click', () => {
        if (window.innerWidth >= 768) {
            // Desktop davranışı
            body.classList.toggle('sidebar-collapsed');
        } else {
            // Mobil davranışı
            body.classList.toggle('sidebar-active');
            
            // Overlay oluştur ve yönet
            let overlay = document.querySelector('.sidebar-overlay');
            if (!overlay) {
                overlay = document.createElement('div');
                overlay.className = 'sidebar-overlay';
                document.body.appendChild(overlay);
                
                overlay.addEventListener('click', () => {
                    body.classList.remove('sidebar-active');
                    overlay.style.display = 'none';
                });
            }
            overlay.style.display = body.classList.contains('sidebar-active') ? 'block' : 'none';
        }
    });
}

/**
 * Kullanıcı menüsü işlevselliği
 */
function initUserMenu() {
    const userMenu = document.getElementById('user-menu');
    const userMenuButton = userMenu.querySelector('button');
    
    // Dropdown menüyü oluştur
    const dropdown = document.createElement('div');
    dropdown.className = 'user-menu-dropdown mt-2 py-2';
    dropdown.innerHTML = `
        <a href="#" class="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100">
            <i class="fas fa-user mr-2"></i> Profil
        </a>
        <a href="#" class="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100">
            <i class="fas fa-cog mr-2"></i> Ayarlar
        </a>
        <hr class="my-2 border-gray-200">
        <a href="login.html" class="block px-4 py-2 text-sm text-red-600 hover:bg-gray-100">
            <i class="fas fa-sign-out-alt mr-2"></i> Çıkış Yap
        </a>
    `;
    userMenu.appendChild(dropdown);
    
    // Tıklama olayını yönet
    userMenuButton.addEventListener('click', (e) => {
        e.stopPropagation();
        dropdown.classList.toggle('active');
    });
    
    // Dışarı tıklandığında menüyü kapat
    document.addEventListener('click', () => {
        dropdown.classList.remove('active');
    });
}

/**
 * Responsive davranış
 */
function initResponsive() {
    const mediaQuery = window.matchMedia('(min-width: 768px)');
    
    function handleResize(e) {
        const body = document.body;
        const overlay = document.querySelector('.sidebar-overlay');
        
        if (e.matches) {
            // Desktop görünümüne geç
            body.classList.remove('sidebar-active');
            if (overlay) {
                overlay.style.display = 'none';
            }
        } else {
            // Mobil görünümüne geç
            body.classList.remove('sidebar-collapsed');
        }
    }
    
    mediaQuery.addListener(handleResize);
    handleResize(mediaQuery);
}

/**
 * Projeler dropdown işlevselliği
 */
function initProjectsDropdown() {
    const projectsDropdownBtn = document.getElementById('projects-dropdown-btn');
    const projectsDropdownContent = document.getElementById('projects-dropdown-content');
    const chevronIcon = projectsDropdownBtn.querySelector('.fa-chevron-down');
    
    projectsDropdownBtn.addEventListener('click', () => {
        projectsDropdownContent.classList.toggle('hidden');
        chevronIcon.classList.toggle('rotate-180');
    });
    
    // Kayıtlı projeleri yükle
    loadProjects();
}

/**
 * Proje oluşturma işlevselliği
 */
function initProjectCreation() {
    const createProjectButtons = document.querySelectorAll('#create-project-btn, #no-projects-message button');
    const modal = document.getElementById('create-project-modal');
    const modalContent = document.getElementById('modal-content');
    const modalOverlay = document.getElementById('modal-overlay');
    const closeModalButton = document.getElementById('close-modal');
    const cancelButton = document.getElementById('cancel-project');
    const createProjectForm = document.getElementById('create-project-form');
    
    // Modal'ı aç
    createProjectButtons.forEach(button => {
        button.addEventListener('click', () => {
            modal.classList.remove('hidden');
            // Animasyon için setTimeout kullan
            setTimeout(() => {
                modalContent.classList.remove('scale-95', 'opacity-0');
                modalContent.classList.add('scale-100', 'opacity-100');
            }, 10);
        });
    });
    
    // Modal'ı kapat
    function closeModal() {
        modalContent.classList.remove('scale-100', 'opacity-100');
        modalContent.classList.add('scale-95', 'opacity-0');
        
        setTimeout(() => {
            modal.classList.add('hidden');
            createProjectForm.reset();
        }, 300);
    }
    
    closeModalButton.addEventListener('click', closeModal);
    cancelButton.addEventListener('click', closeModal);
    modalOverlay.addEventListener('click', closeModal);
    
    // Form gönderimi
    createProjectForm.addEventListener('submit', (e) => {
        e.preventDefault();
        
        const projectName = document.getElementById('project-name').value;
        const projectDescription = document.getElementById('project-description').value;
        const projectDeadline = document.getElementById('project-deadline').value;
        
        // Projeyi kaydet
        saveProject({
            id: Date.now(), // Basit bir ID oluştur
            name: projectName,
            description: projectDescription,
            deadline: projectDeadline,
            createdAt: new Date().toISOString(),
            tasks: []
        });
        
        // Modal'ı kapat
        closeModal();
    });
}

/**
 * Projeleri localStorage'dan yükle
 */
function loadProjects() {
    const projects = getProjects();
    const projectsContainer = document.getElementById('projects-container');
    const noProjectsMessage = document.getElementById('no-projects-message');
    const projectsDropdownContent = document.getElementById('projects-dropdown-content').querySelector('ul');
    
    // Sidebar'daki projeleri güncelle
    projectsDropdownContent.innerHTML = '';
    
    if (projects.length === 0) {
        projectsDropdownContent.innerHTML = `
            <li class="text-sm">
                <span class="text-gray-500 italic">Henüz proje bulunmuyor</span>
            </li>
        `;
        
        noProjectsMessage.classList.remove('hidden');
        return;
    }
    
    // Projeleri sidebar'a ekle
    projects.forEach(project => {
        const projectItem = document.createElement('li');
        projectItem.innerHTML = `
            <a href="project.html?id=${project.id}" class="block py-1 text-sm text-gray-700 hover:text-primary-500">
                <i class="fas fa-folder text-xs mr-1"></i> ${project.name}
            </a>
        `;
        projectsDropdownContent.appendChild(projectItem);
    });
    
    // Proje sayfasındaki proje listesini güncelle
    noProjectsMessage.classList.add('hidden');
    
    // Önceki projeleri temizle (ilk çocuk düğümünü tutma)
    while (projectsContainer.childNodes.length > 1) {
        projectsContainer.removeChild(projectsContainer.lastChild);
    }
    
    // Projeleri ekle
    projects.forEach(project => {
        const projectCard = document.createElement('div');
        projectCard.className = 'bg-white p-4 rounded-lg shadow-sm border border-gray-100 hover:shadow-md transition-all duration-200';
        
        // Bitiş tarihi varsa formatla
        let deadlineHtml = '';
        if (project.deadline) {
            const deadlineDate = new Date(project.deadline);
            const formattedDate = deadlineDate.toLocaleDateString('tr-TR', {day: 'numeric', month: 'short', year: 'numeric'});
            deadlineHtml = `
                <div class="flex items-center text-xs text-gray-500 mt-1">
                    <i class="fas fa-calendar-day mr-1"></i>
                    <span>Bitiş: ${formattedDate}</span>
                </div>
            `;
        }
        
        // Proje oluşturma tarihini formatla
        const createdDate = new Date(project.createdAt);
        const formattedCreatedDate = createdDate.toLocaleDateString('tr-TR', {day: 'numeric', month: 'short', year: 'numeric'});
        
        projectCard.innerHTML = `
            <div class="flex items-start justify-between">
                <div>
                    <h4 class="font-medium text-gray-900">${project.name}</h4>
                    <p class="text-xs text-gray-500 mt-1 line-clamp-2">${project.description || 'Açıklama yok'}</p>
                    ${deadlineHtml}
                </div>
                <div class="flex flex-col items-end">
                    <span class="text-xs text-gray-500">Oluşturuldu: ${formattedCreatedDate}</span>
                    <div class="flex mt-2">
                        <a href="project.html?id=${project.id}" class="p-1 text-primary-500 hover:text-primary-600" title="Projeyi Görüntüle">
                            <i class="fas fa-eye"></i>
                        </a>
                        <button class="p-1 text-gray-400 hover:text-gray-600 ml-1" title="Daha Fazla">
                            <i class="fas fa-ellipsis-h"></i>
                        </button>
                    </div>
                </div>
            </div>
        `;
        
        projectsContainer.appendChild(projectCard);
    });
}

/**
 * Tüm projeleri al
 */
function getProjects() {
    const projects = localStorage.getItem('projects');
    return projects ? JSON.parse(projects) : [];
}

/**
 * Yeni proje kaydet
 */
function saveProject(project) {
    const projects = getProjects();
    projects.push(project);
    localStorage.setItem('projects', JSON.stringify(projects));
    
    // UI'ı güncelle
    loadProjects();
}

/**
 * Hızlı Notlar işlevselliği
 */
function initQuickNotes() {
    const quickNoteInput = document.getElementById('quickNoteInput');
    const addQuickNoteBtn = document.getElementById('addQuickNoteBtn');
    const quickNotesContainer = document.getElementById('quickNotesContainer');

    // Not ekleme butonu tıklama olayı
    addQuickNoteBtn.addEventListener('click', () => {
        addQuickNote();
    });

    // Enter tuşu ile not ekleme
    quickNoteInput.addEventListener('keypress', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            addQuickNote();
        }
    });

    // Not ekleme fonksiyonu
    function addQuickNote() {
        const content = quickNoteInput.value.trim();
        if (!content) return;

        // API'ye not ekleme isteği
        fetch('/api/quicknotes', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ content: content })
        })
        .then(response => response.json())
        .then(note => {
            // Başarılı ise notu UI'a ekle
            addNoteToUI(note);
            quickNoteInput.value = ''; // Input'u temizle
        })
        .catch(error => {
            console.error('Not eklenirken hata oluştu:', error);
            // Hata mesajını göster
            showNotification('Not eklenirken bir hata oluştu', 'error');
        });
    }

    // Notu UI'a ekleme fonksiyonu
    function addNoteToUI(note) {
        const noteElement = document.createElement('div');
        noteElement.className = 'border-b border-gray-100 pb-4';
        noteElement.innerHTML = `
            <div class="flex items-center space-x-3">
                <div class="flex-shrink-0 h-8 w-8 bg-primary-100 text-primary-500 rounded-full flex items-center justify-center">
                    <i class="fas fa-sticky-note text-xs"></i>
                </div>
                <div class="flex-grow">
                    <p class="text-sm text-gray-700">${note.content}</p>
                    <p class="text-xs text-gray-400 mt-1">${formatDate(note.createdAt)}</p>
                </div>
                <div class="flex-shrink-0">
                    <button onclick="deleteQuickNote(${note.id})" class="text-gray-400 hover:text-red-500 transition-colors duration-200">
                        <i class="fas fa-trash-alt text-xs"></i>
                    </button>
                </div>
            </div>
        `;
        
        // Yeni notu en üste ekle
        if (quickNotesContainer.firstChild) {
            quickNotesContainer.insertBefore(noteElement, quickNotesContainer.firstChild);
        } else {
            quickNotesContainer.appendChild(noteElement);
        }
    }

    // Notları yükleme fonksiyonu
    function loadQuickNotes() {
        fetch('/api/quicknotes')
            .then(response => response.json())
            .then(notes => {
                quickNotesContainer.innerHTML = ''; // Container'ı temizle
                notes.forEach(note => addNoteToUI(note));
            })
            .catch(error => {
                console.error('Notlar yüklenirken hata oluştu:', error);
                quickNotesContainer.innerHTML = '<p class="text-center text-gray-500 py-4">Notlar yüklenemedi</p>';
            });
    }

    // Sayfa yüklendiğinde notları yükle
    loadQuickNotes();
}

// Not silme fonksiyonu (global scope'ta olmalı)
function deleteQuickNote(noteId) {
    if (confirm('Bu notu silmek istediğinizden emin misiniz?')) {
        fetch(`/api/quicknotes/${noteId}`, {
            method: 'DELETE'
        })
        .then(response => {
            if (response.ok) {
                // UI'dan notu kaldır
                const noteElement = document.querySelector(`[data-note-id="${noteId}"]`);
                if (noteElement) {
                    noteElement.remove();
                }
                showNotification('Not başarıyla silindi', 'success');
            } else {
                throw new Error('Not silinirken bir hata oluştu');
            }
        })
        .catch(error => {
            console.error('Not silinirken hata oluştu:', error);
            showNotification('Not silinirken bir hata oluştu', 'error');
        });
    }
}

// Tarih formatlama yardımcı fonksiyonu
function formatDate(dateString) {
    const date = new Date(dateString);
    const now = new Date();
    const diffTime = Math.abs(now - date);
    const diffMinutes = Math.floor(diffTime / (1000 * 60));
    const diffHours = Math.floor(diffTime / (1000 * 60 * 60));
    const diffDays = Math.floor(diffTime / (1000 * 60 * 60 * 24));

    if (diffMinutes < 60) {
        return `${diffMinutes} dakika önce`;
    } else if (diffHours < 24) {
        return `${diffHours} saat önce`;
    } else if (diffDays < 30) {
        return `${diffDays} gün önce`;
    } else {
        return date.toLocaleDateString('tr-TR', { 
            day: 'numeric', 
            month: 'long', 
            year: 'numeric'
        });
    }
}

// Bildirim gösterme fonksiyonu
function showNotification(message, type = 'success') {
    const notification = document.createElement('div');
    notification.className = `fixed bottom-4 right-4 px-6 py-3 rounded-lg shadow-lg ${
        type === 'success' ? 'bg-green-500' : 'bg-red-500'
    } text-white z-50 transform transition-all duration-300 translate-y-full opacity-0`;
    
    notification.innerHTML = message;
    document.body.appendChild(notification);
    
    // Animasyon için setTimeout
    setTimeout(() => {
        notification.classList.remove('translate-y-full', 'opacity-0');
    }, 100);
    
    // 3 saniye sonra bildirimi kaldır
    setTimeout(() => {
        notification.classList.add('translate-y-full', 'opacity-0');
        setTimeout(() => {
            notification.remove();
        }, 300);
    }, 3000);
} 