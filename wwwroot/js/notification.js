

// DOM Elements
const notificationBell = document.getElementById('notificationBell');
const notificationBadge = document.getElementById('notificationBadge');
const addNotificationBtn = document.getElementById('addNotificationBtn');

// Initialize Bootstrap popover
let popover;

function initPopover() {
    // Destroy existing popover if it exists
    if (popover) {
        popover.dispose();
    }

    // Create new popover
    popover = new bootstrap.Popover(notificationBell, {
        html: true,
        content: generateNotificationContent(),
        template: '<div class="popover notification-popover" role="tooltip"><div class="popover-arrow"></div><div class="popover-body"></div></div>',
        placement: 'bottom',
        trigger: 'click',
        boundary: 'viewport'
    });

    // Update badge count
    updateNotificationBadge();
}

// Generate notification content
function generateNotificationContent() {
    // Clone the template
    const template = document.getElementById('notificationTemplate').cloneNode(true);
    template.style.display = 'block';

    // Get the notification list element
    const notificationList = template.querySelector('#notificationList');
    const emptyNotifications = template.querySelector('#emptyNotifications');

    // Clear the list
    notificationList.innerHTML = '';

    // Check if there are notifications
    if (notificationsData.length === 0) {
        emptyNotifications.classList.remove('d-none');
    } else {
        emptyNotifications.classList.add('d-none');

        // Add notifications to the list
        notificationsData.forEach(notification => {
            const notificationItem = document.createElement('li');
            notificationItem.className = `notification-item ${notification.read ? '' : 'unread'}`;
            notificationItem.dataset.id = notification.id;

            notificationItem.innerHTML = `
                <div class="notification-icon ${notification.type}">
                  <i class="${notification.icon}"></i>
                </div>
                <div class="notification-content">
                  <p class="notification-message">${notification.message}</p>
                  <span class="notification-time">${notification.time}</span>
                </div>
                <div class="notification-actions">
                  ${!notification.read ?
                    `<button class="notification-action mark-read" title="Mark as read" onclick="markNotificationAsRead(${notification.id})">
                      <i class="fas fa-check"></i>
                    </button>` : ''}
                  <button class="notification-action delete" title="Delete" onclick="deleteNotification(${notification.id})">
                    <i class="fas fa-times"></i>
                  </button>
                </div>
              `;

            notificationList.appendChild(notificationItem);
        });
    }

    // Add event listener to "Mark all as read" button
    const markAllReadBtn = template.querySelector('#markAllRead');
    markAllReadBtn.addEventListener('click', function () {
        markAllNotificationsAsRead();
        // Hide and show the popover to refresh content
        popover.hide();
        setTimeout(() => {
            initPopover();
            popover.show();
        }, 100);
    });

    return template;
}

// Update notification badge count
function updateNotificationBadge() {
    const unreadCount = notificationsData.filter(n => !n.read).length;

    if (unreadCount > 0) {
        notificationBadge.textContent = unreadCount;
        notificationBadge.style.display = 'flex';
    } else {
        notificationBadge.style.display = 'none';
    }
}

// Mark a notification as read
function markNotificationAsRead(id) {
    const notification = notificationsData.find(n => n.id === id);
    if (notification) {
        notification.read = true;
        // Hide and show the popover to refresh content
        popover.hide();
        setTimeout(() => {
            initPopover();
            popover.show();
        }, 100);
    }
}

// Delete a notification
function deleteNotification(id) {
    const index = notificationsData.findIndex(n => n.id === id);
    if (index !== -1) {
        notificationsData.splice(index, 1);
        // Hide and show the popover to refresh content
        popover.hide();
        setTimeout(() => {
            initPopover();
            popover.show();
        }, 100);
    }
}

// Mark all notifications as read
function markAllNotificationsAsRead() {
    notificationsData.forEach(notification => {
        notification.read = true;
    });
    updateNotificationBadge();
}

// Add a new notification (for demo purposes)
function addNewNotification() {
    const types = ['success', 'danger', 'warning', 'info'];
    const icons = ['fas fa-check', 'fas fa-times', 'fas fa-exclamation', 'fas fa-info'];
    const messages = [
        'New comment on your post.',
        'Your project has been approved.',
        'System maintenance scheduled for tomorrow.',
        'You have a new message from the admin.'
    ];

    const randomIndex = Math.floor(Math.random() * types.length);

    const newNotification = {
        id: Date.now(),
        type: types[randomIndex],
        icon: icons[randomIndex],
        message: messages[randomIndex],
        time: 'Just now',
        read: false
    };

    notificationsData.unshift(newNotification);

    // Refresh the popover
    initPopover();

    // Show a browser notification if supported
    if ('Notification' in window && Notification.permission === 'granted') {
        new Notification('New Notification', {
            body: newNotification.message,
            icon: '/favicon.ico'
        });
    }
}

// Event Listeners
document.addEventListener('DOMContentLoaded', function () {
    // Initialize the popover
    initPopover();

    // Add event listener to the "Add New Notification" button
    addNotificationBtn.addEventListener('click', function () {
        addNewNotification();
    });

    // Make these functions available globally
    window.markNotificationAsRead = markNotificationAsRead;
    window.deleteNotification = deleteNotification;
    window.markAllNotificationsAsRead = markAllNotificationsAsRead;

    // Request notification permission
    if ('Notification' in window && Notification.permission !== 'denied') {
        Notification.requestPermission();
    }
});