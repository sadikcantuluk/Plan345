document.addEventListener('DOMContentLoaded', () => {
    const taskCards = document.querySelectorAll('.task-card');
    const tasksContainers = document.querySelectorAll('.tasks-container');
    const addTaskBtns = document.querySelectorAll('.add-task-btn');

    // Drag and Drop işlevselliği
    taskCards.forEach(card => {
        card.addEventListener('dragstart', dragStart);
        card.addEventListener('dragend', dragEnd);
    });

    tasksContainers.forEach(container => {
        container.addEventListener('dragover', dragOver);
        container.addEventListener('drop', drop);
    });

    // Yeni görev ekleme
    addTaskBtns.forEach(btn => {
        btn.addEventListener('click', createNewTask);
    });

    function dragStart(e) {
        e.target.classList.add('dragging');
    }

    function dragEnd(e) {
        e.target.classList.remove('dragging');
    }

    function dragOver(e) {
        e.preventDefault();
        const container = e.target.closest('.tasks-container');
        container.classList.add('drag-over');
    }

    function drop(e) {
        e.preventDefault();
        const container = e.target.closest('.tasks-container');
        container.classList.remove('drag-over');
        
        const draggingCard = document.querySelector('.dragging');
        if (draggingCard && container) {
            container.appendChild(draggingCard);
            updateTaskStatus(draggingCard, container);
        }
    }

    function createNewTask(e) {
        const column = e.target.closest('.board-column');
        const container = column.querySelector('.tasks-container');
        
        const taskCard = document.createElement('div');
        taskCard.className = 'task-card';
        taskCard.draggable = true;
        
        const today = new Date();
        const dueDate = new Date(today);
        dueDate.setDate(today.getDate() + 7);
        
        taskCard.innerHTML = `
            <h3 class="task-title">Yeni Görev</h3>
            <p class="task-description">Görev açıklaması ekleyin</p>
            <div class="task-meta">
                <span class="due-date">Son Tarih: ${dueDate.getDate()} ${dueDate.toLocaleString('tr-TR', { month: 'long' })}</span>
                <span class="assigned-to">Atanmadı</span>
            </div>
        `;
        
        container.appendChild(taskCard);
        taskCard.addEventListener('dragstart', dragStart);
        taskCard.addEventListener('dragend', dragEnd);
        
        // Yeni eklenen görevin düzenlenebilir olması
        makeTaskEditable(taskCard);
    }

    function makeTaskEditable(taskCard) {
        taskCard.addEventListener('dblclick', (e) => {
            const element = e.target;
            if (element.classList.contains('task-title') || 
                element.classList.contains('task-description')) {
                const originalText = element.textContent;
                element.contentEditable = true;
                element.focus();
                
                element.addEventListener('blur', () => {
                    element.contentEditable = false;
                    if (element.textContent.trim() === '') {
                        element.textContent = originalText;
                    }
                });
            }
        });
    }

    function updateTaskStatus(taskCard, container) {
        const column = container.closest('.board-column');
        const dateSpan = taskCard.querySelector('.due-date');
        
        if (column.id === 'done') {
            const today = new Date();
            dateSpan.textContent = `Tamamlandı: ${today.getDate()} ${today.toLocaleString('tr-TR', { month: 'long' })}`;
        }
    }

    // Mevcut kartları düzenlenebilir yap
    taskCards.forEach(makeTaskEditable);
}); 