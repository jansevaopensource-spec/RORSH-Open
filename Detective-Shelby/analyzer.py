"""
AI vs Real Image Forensic Analyzer
Analyzes images across multiple forensic dimensions.
"""

import numpy as np
import cv2
from PIL import Image, ExifTags
import piexif


import json
import io
import base64
from collections import defaultdict
import warnings
warnings.filterwarnings('ignore')


class ForensicAnalyzer:
    def __init__(self):
        self.results = {}

    def analyze(self, image_path):
        """Run complete forensic analysis on an image."""
        self.results = {
            'metadata': {},
            'jpeg_analysis': {},
            'noise_analysis': {},
            'frequency_analysis': {},
            'color_analysis': {},
            'resampling': {},
            'local_patches': {},
            'texture_analysis': {},
            'edge_analysis': {},
            'overall_score': 0,
            'verdict': '',
            'confidence': 0,
            'heatmaps': {}
        }

        # Load image
        img_pil = Image.open(image_path)
        img_cv = cv2.imread(image_path)
        img_rgb = cv2.cvtColor(img_cv, cv2.COLOR_BGR2RGB) if img_cv is not None else np.array(img_pil.convert('RGB'))
        img_gray = cv2.cvtColor(img_rgb, cv2.COLOR_RGB2GRAY)

        # Run all analyses
        self._analyze_metadata(img_pil, image_path)
        self._analyze_jpeg(image_path, img_rgb)
        self._analyze_noise(img_rgb, img_gray)
        self._analyze_frequency(img_gray)
        self._analyze_color(img_rgb)
        self._detect_resampling(img_gray)
        self._analyze_local_patches(img_rgb, img_gray)
        self._analyze_texture(img_gray)
        self._analyze_edges(img_gray)
        self._compute_overall_score()

        return self.results

    def _analyze_metadata(self, img_pil, image_path):
        """Extract and analyze image metadata."""
        metadata = {
            'format': img_pil.format,
            'mode': img_pil.mode,
            'size': img_pil.size,
            'has_exif': False,
            'camera_info': {},
            'software': None,
            'timestamp': None,
            'gps': None,
            'red_flags': [],
            'green_flags': []
        }

        try:
            exif_dict = piexif.load(image_path)
            metadata['has_exif'] = True

            # Extract key fields
            ifd = exif_dict.get('0th', {})
            exif_ifd = exif_dict.get('Exif', {})
            gps_ifd = exif_dict.get('GPS', {})

            # Camera make/model
            make = ifd.get(piexif.ImageIFD.Make, b'').decode('utf-8', errors='ignore').strip()
            model = ifd.get(piexif.ImageIFD.Model, b'').decode('utf-8', errors='ignore').strip()
            if make or model:
                metadata['camera_info']['make'] = make
                metadata['camera_info']['model'] = model
                metadata['green_flags'].append(f"Camera detected: {make} {model}")

            # Software
            software = ifd.get(piexif.ImageIFD.Software, b'').decode('utf-8', errors='ignore').strip()
            if software:
                metadata['software'] = software
                ai_software = ['midjourney', 'dall-e', 'stable diffusion', 'flux', 'ideogram', 
                              'firefly', 'bing image', 'leonardo', 'nightcafe']
                if any(s in software.lower() for s in ai_software):
                    metadata['red_flags'].append(f"AI software detected: {software}")
                elif any(s in software.lower() for s in ['photoshop', 'gimp', 'paint']):
                    metadata['red_flags'].append(f"Editing software: {software}")
                else:
                    metadata['green_flags'].append(f"Software: {software}")

            # Timestamp
            date_time = ifd.get(piexif.ImageIFD.DateTime, b'').decode('utf-8', errors='ignore')
            if date_time:
                metadata['timestamp'] = date_time

            # Camera settings
            if piexif.ExifIFD.FNumber in exif_ifd:
                metadata['camera_info']['aperture'] = exif_ifd[piexif.ExifIFD.FNumber]
            if piexif.ExifIFD.ISOSpeedRatings in exif_ifd:
                metadata['camera_info']['iso'] = exif_ifd[piexif.ExifIFD.ISOSpeedRatings]
            if piexif.ExifIFD.ExposureTime in exif_ifd:
                exp = exif_ifd[piexif.ExifIFD.ExposureTime]
                if isinstance(exp, tuple) and len(exp) == 2 and exp[1] != 0:
                    metadata['camera_info']['shutter_speed'] = f"{exp[0]}/{exp[1]}"
            if piexif.ExifIFD.FocalLength in exif_ifd:
                metadata['camera_info']['focal_length'] = exif_ifd[piexif.ExifIFD.FocalLength]

            # GPS
            if gps_ifd:
                metadata['gps'] = 'Present'
                metadata['green_flags'].append("GPS data present")

            # Check for missing camera metadata
            if not make and not model and not software:
                metadata['red_flags'].append("No camera or software metadata found")

        except Exception as e:
            metadata['has_exif'] = False
            metadata['red_flags'].append(f"No readable EXIF data: {str(e)}")

        # Check for PNG text chunks
        if img_pil.format == 'PNG':
            try:
                if hasattr(img_pil, 'text') and img_pil.text:
                    metadata['png_text'] = dict(img_pil.text)
                    if any('ai' in str(v).lower() or 'generated' in str(v).lower() for v in img_pil.text.values()):
                        metadata['red_flags'].append("PNG text mentions AI/generation")
            except:
                pass

        # Score metadata
        score = 0
        if metadata['camera_info'].get('make') or metadata['camera_info'].get('model'):
            score += 30
        if metadata['camera_info'].get('iso') or metadata['camera_info'].get('aperture'):
            score += 20
        if metadata['gps']:
            score += 10
        if metadata['software'] and not any(r.startswith('AI') or r.startswith('Editing') for r in metadata['red_flags']):
            score += 15
        if len(metadata['red_flags']) > 2:
            score -= 20
        if not metadata['has_exif']:
            score -= 25

        metadata['score'] = max(0, min(100, score))
        self.results['metadata'] = metadata

    def _analyze_jpeg(self, image_path, img_rgb):
        """Analyze JPEG compression characteristics."""
        jpeg_data = {
            'is_jpeg': False,
            'quality_estimate': None,
            'q_table_variance': None,
            'double_compression_detected': False,
            'chroma_subsampling': None,
            'compression_score': 50,
            'details': []
        }

        try:
            with open(image_path, 'rb') as f:
                data = f.read()

            # Check if JPEG
            if data[:2] == b'\xff\xd8':
                jpeg_data['is_jpeg'] = True

                # Find DQT segments
                pos = 2
                q_tables = []
                while pos < len(data) - 1:
                    if data[pos] == 0xFF:
                        marker = data[pos + 1]
                        if marker == 0xDB:  # DQT
                            length = int.from_bytes(data[pos+2:pos+4], 'big')
                            qt_data = data[pos+4:pos+2+length]
                            i = 0
                            while i < len(qt_data):
                                precision = (qt_data[i] >> 4) & 0x0F
                                table_id = qt_data[i] & 0x0F
                                i += 1
                                if precision == 0:
                                    table = list(qt_data[i:i+64])
                                    i += 64
                                else:
                                    table = []
                                    for j in range(64):
                                        table.append(int.from_bytes(qt_data[i:i+2], 'big'))
                                        i += 2
                                q_tables.append(table)
                        elif marker == 0xD9:  # EOI
                            break
                        elif marker not in [0x00, 0x01] and marker < 0xD0 or marker > 0xD9:
                            if pos + 3 < len(data):
                                length = int.from_bytes(data[pos+2:pos+4], 'big')
                                pos += length + 1
                                continue
                    pos += 1

                # Analyze quantization tables
                if q_tables:
                    # Estimate quality from Q-tables
                    std_lum = [16,11,10,16,24,40,51,61,12,12,14,19,26,58,60,55,
                              14,13,16,24,40,57,69,56,14,17,22,29,51,87,80,62,
                              18,22,37,56,68,109,103,77,24,35,55,64,81,104,113,92,
                              49,64,78,87,103,121,120,101,72,92,95,98,112,100,103,99]

                    qt = q_tables[0]
                    ratios = []
                    for i in range(64):
                        if std_lum[i] > 0:
                            ratios.append(qt[i] / std_lum[i])

                    if ratios:
                        avg_ratio = np.mean(ratios)
                        # Approximate quality mapping
                        if avg_ratio <= 0.5:
                            quality = 95
                        elif avg_ratio <= 0.7:
                            quality = 90
                        elif avg_ratio <= 1.0:
                            quality = 85
                        elif avg_ratio <= 1.5:
                            quality = 75
                        elif avg_ratio <= 2.0:
                            quality = 65
                        elif avg_ratio <= 3.0:
                            quality = 50
                        else:
                            quality = 30
                        jpeg_data['quality_estimate'] = quality

                        # Variance in Q-table indicates camera vs re-encoded
                        q_var = np.var(qt)
                        jpeg_data['q_table_variance'] = round(q_var, 2)

                        # Camera Q-tables often have specific patterns
                        # High variance in Q-table suggests re-encoding or AI
                        if q_var > 500:
                            jpeg_data['details'].append("High Q-table variance — possible re-encoding")
                            jpeg_data['compression_score'] -= 15
                        else:
                            jpeg_data['details'].append("Q-table pattern consistent with camera firmware")
                            jpeg_data['compression_score'] += 15

                        if quality > 95:
                            jpeg_data['details'].append("Very high quality — possible AI generation saved at max quality")
                            jpeg_data['compression_score'] -= 10
                        elif quality < 70:
                            jpeg_data['details'].append("Low quality compression — social media or re-saved")

                # Detect double compression via DCT analysis
                img_gray = cv2.cvtColor(img_rgb, cv2.COLOR_RGB2GRAY)
                h, w = img_gray.shape
                h8, w8 = (h // 8) * 8, (w // 8) * 8
                img_cropped = img_gray[:h8, :w8]

                # Extract DCT coefficients for first 1000 blocks
                blocks = []
                count = 0
                for i in range(0, h8, 8):
                    for j in range(0, w8, 8):
                        if count >= 1000:
                            break
                        block = img_cropped[i:i+8, j:j+8].astype(np.float32) - 128
                        dct = cv2.dct(block)
                        blocks.append(dct.flatten())
                        count += 1
                    if count >= 1000:
                        break

                if blocks:
                    blocks = np.array(blocks)
                    # Benford's law analysis on DCT coefficients
                    ac_coeffs = np.abs(blocks[:, 1:])  # Skip DC
                    ac_flat = ac_coeffs.flatten()
                    ac_flat = ac_flat[ac_flat > 0]

                    if len(ac_flat) > 100:
                        first_digits = np.array([int(str(int(x))[0]) for x in ac_flat if x >= 1])
                        if len(first_digits) > 100:
                            benford_expected = np.array([30.1, 17.6, 12.5, 9.7, 7.9, 6.7, 5.8, 5.1, 4.6])
                            hist, _ = np.histogram(first_digits, bins=range(1, 11), density=True)
                            hist = hist * 100

                            if len(hist) == 9:
                                chi2 = np.sum((hist - benford_expected) ** 2 / benford_expected)
                                if chi2 > 50:
                                    jpeg_data['double_compression_detected'] = True
                                    jpeg_data['details'].append("DCT coefficient distribution anomaly — possible double compression")
                                    jpeg_data['compression_score'] -= 20
                                else:
                                    jpeg_data['details'].append("DCT distribution appears natural")
                                    jpeg_data['compression_score'] += 10

                # Chroma subsampling check
                if img_rgb.shape[2] == 3:
                    yuv = cv2.cvtColor(img_rgb, cv2.COLOR_RGB2YCrCb)
                    y, cr, cb = yuv[:,:,0], yuv[:,:,1], yuv[:,:,2]

                    # Compare chroma resolution
                    cr_down = cv2.resize(cr, (cr.shape[1]//2, cr.shape[0]//2))
                    cr_up = cv2.resize(cr_down, (cr.shape[1], cr.shape[0]))
                    cr_diff = np.mean(np.abs(cr.astype(float) - cr_up.astype(float)))

                    if cr_diff < 2:
                        jpeg_data['chroma_subsampling'] = "4:2:0 or 4:2:2"
                        jpeg_data['details'].append("Chroma subsampling detected — typical of camera JPEGs")
                        jpeg_data['compression_score'] += 10
                    else:
                        jpeg_data['chroma_subsampling'] = "4:4:4 (no subsampling)"
                        jpeg_data['details'].append("No chroma subsampling — possible AI or PNG conversion")
                        jpeg_data['compression_score'] -= 5
            else:
                jpeg_data['details'].append("Not a JPEG file — no compression artifacts to analyze")
                jpeg_data['compression_score'] = 50

        except Exception as e:
            jpeg_data['details'].append(f"JPEG analysis error: {str(e)}")

        jpeg_data['compression_score'] = max(0, min(100, jpeg_data['compression_score']))
        self.results['jpeg_analysis'] = jpeg_data

    def _analyze_noise(self, img_rgb, img_gray):
        """Analyze noise characteristics — KEY discriminator for AI vs real."""
        noise_data = {
            'noise_variance': None,
            'noise_spatial_variance': None,
            'noise_autocorr_peak': None,
            'noise_periodicity': None,
            'channel_noise_ratio': None,
            'noise_uniformity': None,
            'noise_score': 50,
            'details': [],
            'heatmap': None
        }

        try:
            # Noise extraction via multi-scale Gaussian filtering (no pywt)
            # This approximates wavelet denoising using scale-space decomposition
            img_f = img_gray.astype(np.float32)

            # Multi-scale denoising: blend multiple Gaussian scales
            g1 = cv2.GaussianBlur(img_f, (3, 3), 0.5)
            g2 = cv2.GaussianBlur(img_f, (5, 5), 1.0)
            g3 = cv2.GaussianBlur(img_f, (7, 7), 1.5)
            g4 = cv2.GaussianBlur(img_f, (9, 9), 2.0)

            # Weighted combination preserves edges better than single Gaussian
            denoised = 0.4 * g1 + 0.3 * g2 + 0.2 * g3 + 0.1 * g4

            noise_residual = img_gray.astype(np.float32) - denoised

            # Global noise variance
            noise_var = np.var(noise_residual)
            noise_data['noise_variance'] = round(noise_var, 4)

            # Spatial variance of noise (should be uniform in real cameras, patchy in AI)
            patch_size = 64
            h, w = noise_residual.shape
            local_vars = []
            for i in range(0, h - patch_size, patch_size):
                for j in range(0, w - patch_size, patch_size):
                    patch = noise_residual[i:i+patch_size, j:j+patch_size]
                    local_vars.append(np.var(patch))

            if local_vars:
                spatial_var = np.var(local_vars)
                noise_data['noise_spatial_variance'] = round(spatial_var, 6)

                # Real cameras have relatively uniform noise
                # AI images often have artificially uniform or patchy noise
                cv_noise = np.std(local_vars) / (np.mean(local_vars) + 1e-10)
                noise_data['noise_uniformity'] = round(cv_noise, 4)

                if cv_noise < 0.3:
                    noise_data['details'].append("Noise is very uniform — suspicious for AI (too clean)")
                    noise_data['noise_score'] -= 20
                elif cv_noise > 1.0:
                    noise_data['details'].append("Noise highly variable spatially — possible manipulation")
                    noise_data['noise_score'] -= 10
                else:
                    noise_data['details'].append("Noise spatial distribution appears natural")
                    noise_data['noise_score'] += 15

            # Noise autocorrelation (real sensor noise has specific correlation patterns)
            noise_centered = noise_residual - np.mean(noise_residual)
            autocorr = np.correlate(noise_centered.flatten(), noise_centered.flatten(), mode='full')
            autocorr = autocorr[len(autocorr)//2:]
            autocorr = autocorr / autocorr[0]

            # Check for periodicity in noise
            if len(autocorr) > 100:
                peaks = []
                for i in range(10, min(500, len(autocorr) - 1)):
                    if autocorr[i] > autocorr[i-1] and autocorr[i] > autocorr[i+1] and autocorr[i] > 0.05:
                        peaks.append((i, autocorr[i]))

                if peaks:
                    noise_data['noise_autocorr_peak'] = round(float(peaks[0][1]), 4)
                    # Strong periodic peaks suggest artificial noise or processing
                    if peaks[0][1] > 0.2:
                        noise_data['details'].append("Noise shows periodic correlation — possible artificial noise")
                        noise_data['noise_score'] -= 15
                    else:
                        noise_data['details'].append("Noise autocorrelation appears random")
                        noise_data['noise_score'] += 10

            # Channel-specific noise analysis
            r_noise = img_rgb[:,:,0].astype(float) - cv2.GaussianBlur(img_rgb[:,:,0].astype(float), (5,5), 1)
            g_noise = img_rgb[:,:,1].astype(float) - cv2.GaussianBlur(img_rgb[:,:,1].astype(float), (5,5), 1)
            b_noise = img_rgb[:,:,2].astype(float) - cv2.GaussianBlur(img_rgb[:,:,2].astype(float), (5,5), 1)

            r_var, g_var, b_var = np.var(r_noise), np.var(g_noise), np.var(b_noise)
            channel_vars = [r_var, g_var, b_var]

            if max(channel_vars) > 0:
                cv_channels = np.std(channel_vars) / np.mean(channel_vars)
                noise_data['channel_noise_ratio'] = round(cv_channels, 4)

                # Real cameras: green channel typically has less noise (more pixels)
                # AI: channels often have similar noise
                if g_var < r_var * 0.8 and g_var < b_var * 0.8:
                    noise_data['details'].append("Green channel noise lower — consistent with Bayer pattern")
                    noise_data['noise_score'] += 15
                elif cv_channels < 0.1:
                    noise_data['details'].append("Channel noise very similar — suspicious for AI")
                    noise_data['noise_score'] -= 15

            # Generate noise heatmap
            patch_size = 32
            h, w = noise_residual.shape
            heatmap = np.zeros((h // patch_size, w // patch_size))
            for i in range(0, h - patch_size, patch_size):
                for j in range(0, w - patch_size, patch_size):
                    patch = noise_residual[i:i+patch_size, j:j+patch_size]
                    heatmap[i//patch_size, j//patch_size] = np.var(patch)

            # Normalize and colorize
            heatmap_norm = (heatmap - heatmap.min()) / (heatmap.max() - heatmap.min() + 1e-10)
            heatmap_color = cv2.applyColorMap((heatmap_norm * 255).astype(np.uint8), cv2.COLORMAP_JET)
            noise_data['heatmap'] = self._img_to_base64(heatmap_color)

            # Overall noise assessment
            if noise_var < 5:
                noise_data['details'].append("Very low noise — image may be denoised or AI-generated")
                noise_data['noise_score'] -= 15
            elif noise_var > 100:
                noise_data['details'].append("Very high noise — possible high-ISO real photo or heavy grain")
                noise_data['noise_score'] += 5

        except Exception as e:
            noise_data['details'].append(f"Noise analysis error: {str(e)}")

        noise_data['noise_score'] = max(0, min(100, noise_data['noise_score']))
        self.results['noise_analysis'] = noise_data

    def _analyze_frequency(self, img_gray):
        """Analyze frequency domain characteristics."""
        freq_data = {
            'high_freq_energy': None,
            'low_freq_ratio': None,
            'spectral_slope': None,
            'periodic_peaks': None,
            'freq_score': 50,
            'details': [],
            'spectrum_image': None
        }

        try:
            # 2D FFT
            f_transform = np.fft.fft2(img_gray.astype(float))
            f_shift = np.fft.fftshift(f_transform)
            magnitude = np.abs(f_shift)

            # Log scale for visualization
            magnitude_log = np.log(magnitude + 1)

            # Create spectrum image
            spec_norm = (magnitude_log - magnitude_log.min()) / (magnitude_log.max() - magnitude_log.min())
            spec_img = (spec_norm * 255).astype(np.uint8)
            spec_color = cv2.applyColorMap(spec_img, cv2.COLORMAP_VIRIDIS)
            freq_data['spectrum_image'] = self._img_to_base64(spec_color)

            # Radial frequency distribution
            h, w = magnitude.shape
            center_y, center_x = h // 2, w // 2

            y, x = np.ogrid[:h, :w]
            r = np.sqrt((x - center_x)**2 + (y - center_y)**2).astype(int)

            max_r = min(h, w) // 2
            radial_mean = np.zeros(max_r)
            for radius in range(max_r):
                mask = (r >= radius) & (r < radius + 1)
                if np.any(mask):
                    radial_mean[radius] = np.mean(magnitude[mask])

            # High frequency energy (outer 30% of spectrum)
            high_freq_start = int(max_r * 0.7)
            if high_freq_start < max_r:
                high_freq_energy = np.sum(radial_mean[high_freq_start:])
                total_energy = np.sum(radial_mean)
                if total_energy > 0:
                    freq_data['high_freq_energy'] = round(high_freq_energy / total_energy * 100, 2)
                    freq_data['low_freq_ratio'] = round(100 - freq_data['high_freq_energy'], 2)

            # Spectral slope (natural images follow power law ~ 1/f^alpha)
            valid = radial_mean > 0
            if np.sum(valid) > 10:
                log_r = np.log(np.arange(max_r)[valid] + 1)
                log_mag = np.log(radial_mean[valid])

                if len(log_r) > 5:
                    # Manual linear regression (no scipy)
                    n = len(log_r)
                    x_mean = np.mean(log_r)
                    y_mean = np.mean(log_mag)
                    numerator = np.sum((log_r - x_mean) * (log_mag - y_mean))
                    denominator = np.sum((log_r - x_mean) ** 2)
                    slope = numerator / (denominator + 1e-10)
                    intercept = y_mean - slope * x_mean
                    # R-squared
                    y_pred = slope * log_r + intercept
                    ss_res = np.sum((log_mag - y_pred) ** 2)
                    ss_tot = np.sum((log_mag - y_mean) ** 2)
                    r_value = np.sqrt(1 - ss_res / (ss_tot + 1e-10)) if ss_tot > 0 else 0
                    freq_data['spectral_slope'] = round(slope, 4)

                    # Natural images: slope typically -1.5 to -2.5
                    # AI images: often different slope due to generator artifacts
                    if -2.8 < slope < -1.2:
                        freq_data['details'].append(f"Spectral slope ({slope:.2f}) consistent with natural image")
                        freq_data['freq_score'] += 15
                    elif slope > -1.0:
                        freq_data['details'].append(f"Shallow spectral slope ({slope:.2f}) — possible smoothing/AI")
                        freq_data['freq_score'] -= 15
                    else:
                        freq_data['details'].append(f"Steep spectral slope ({slope:.2f}) — possible over-processing")
                        freq_data['freq_score'] -= 5

            # Detect periodic peaks (grid artifacts from generators)
            # Check for peaks in horizontal and vertical frequency lines
            center_slice_h = magnitude[center_y, :]
            center_slice_v = magnitude[:, center_x]

            # Exclude DC component
            center_slice_h = center_slice_h[center_x+10:]
            center_slice_v = center_slice_v[center_y+10:]

            peaks_h = 0
            peaks_v = 0

            if len(center_slice_h) > 20:
                for i in range(1, len(center_slice_h) - 1):
                    if center_slice_h[i] > center_slice_h[i-1] and center_slice_h[i] > center_slice_h[i+1]:
                        if center_slice_h[i] > np.mean(center_slice_h) * 2:
                            peaks_h += 1

            if len(center_slice_v) > 20:
                for i in range(1, len(center_slice_v) - 1):
                    if center_slice_v[i] > center_slice_v[i-1] and center_slice_v[i] > center_slice_v[i+1]:
                        if center_slice_v[i] > np.mean(center_slice_v) * 2:
                            peaks_v += 1

            freq_data['periodic_peaks'] = peaks_h + peaks_v

            if freq_data['periodic_peaks'] > 5:
                freq_data['details'].append(f"Detected {freq_data['periodic_peaks']} periodic frequency peaks — possible generator artifacts")
                freq_data['freq_score'] -= 20
            else:
                freq_data['details'].append("No significant periodic frequency artifacts")
                freq_data['freq_score'] += 10

            # High frequency content assessment
            if freq_data['high_freq_energy'] is not None:
                if freq_data['high_freq_energy'] < 5:
                    freq_data['details'].append("Very low high-frequency content — possibly blurred or AI-smoothed")
                    freq_data['freq_score'] -= 10
                elif freq_data['high_freq_energy'] > 20:
                    freq_data['details'].append("High high-frequency content — sharp real photo or over-sharpened")
                    freq_data['freq_score'] += 5

        except Exception as e:
            freq_data['details'].append(f"Frequency analysis error: {str(e)}")

        freq_data['freq_score'] = max(0, min(100, freq_data['freq_score']))
        self.results['frequency_analysis'] = freq_data

    def _analyze_color(self, img_rgb):
        """Analyze color statistics."""
        color_data = {
            'rgb_means': {},
            'rgb_variances': {},
            'rgb_correlations': {},
            'saturation_mean': None,
            'saturation_std': None,
            'highlight_clipping': None,
            'shadow_detail': None,
            'color_score': 50,
            'details': [],
            'histogram_image': None
        }

        try:
            r, g, b = img_rgb[:,:,0], img_rgb[:,:,1], img_rgb[:,:,2]

            # RGB means and variances
            color_data['rgb_means'] = {
                'R': round(float(np.mean(r)), 2),
                'G': round(float(np.mean(g)), 2),
                'B': round(float(np.mean(b)), 2)
            }
            color_data['rgb_variances'] = {
                'R': round(float(np.var(r)), 2),
                'G': round(float(np.var(g)), 2),
                'B': round(float(np.var(b)), 2)
            }

            # RGB correlations
            rg_corr = np.corrcoef(r.flatten(), g.flatten())[0,1]
            rb_corr = np.corrcoef(r.flatten(), b.flatten())[0,1]
            gb_corr = np.corrcoef(g.flatten(), b.flatten())[0,1]

            color_data['rgb_correlations'] = {
                'R-G': round(float(rg_corr), 4),
                'R-B': round(float(rb_corr), 4),
                'G-B': round(float(gb_corr), 4)
            }

            # Saturation analysis
            hsv = cv2.cvtColor(img_rgb, cv2.COLOR_RGB2HSV)
            saturation = hsv[:,:,1]
            color_data['saturation_mean'] = round(float(np.mean(saturation)), 2)
            color_data['saturation_std'] = round(float(np.std(saturation)), 2)

            # Highlight clipping
            highlights = np.sum((r > 250) & (g > 250) & (b > 250))
            total_pixels = r.size
            color_data['highlight_clipping'] = round(highlights / total_pixels * 100, 4)

            # Shadow detail
            shadows = np.sum((r < 10) & (g < 10) & (b < 10))
            color_data['shadow_detail'] = round(shadows / total_pixels * 100, 4)

            # Generate histogram
            fig_data = self._create_histogram_image(r, g, b)
            color_data['histogram_image'] = fig_data

            # Analysis
            # AI images often have slightly different color correlations
            if abs(rg_corr - gb_corr) < 0.05 and abs(rb_corr - gb_corr) < 0.05:
                color_data['details'].append("RGB correlations very similar — possible AI generation")
                color_data['color_score'] -= 10
            else:
                color_data['details'].append("RGB correlations show natural variation")
                color_data['color_score'] += 10

            if color_data['highlight_clipping'] > 5:
                color_data['details'].append("Significant highlight clipping — possible overexposure or processing")
                color_data['color_score'] -= 5

            if color_data['saturation_std'] < 20:
                color_data['details'].append("Low saturation variation — image may be artificially flat")
                color_data['color_score'] -= 10
            else:
                color_data['details'].append("Good saturation variation")
                color_data['color_score'] += 5

        except Exception as e:
            color_data['details'].append(f"Color analysis error: {str(e)}")

        color_data['color_score'] = max(0, min(100, color_data['color_score']))
        self.results['color_analysis'] = color_data

    def _detect_resampling(self, img_gray):
        """Detect resampling/interpolation artifacts."""
        resample_data = {
            'upscaling_detected': False,
            'interpolation_type': None,
            'resample_confidence': 0,
            'resample_score': 50,
            'details': []
        }

        try:
            # Method: Check for interpolation patterns in second derivative
            # Upscaled images show characteristic patterns in Laplacian

            laplacian = cv2.Laplacian(img_gray.astype(np.float32), cv2.CV_32F)

            # Check for periodic patterns in Laplacian variance along rows/cols
            row_vars = np.var(laplacian, axis=1)
            col_vars = np.var(laplacian, axis=0)

            # Detect periodicity in variance (sign of interpolation)
            def detect_periodicity(signal, max_period=20):
                if len(signal) < max_period * 3:
                    return 0, 0

                # Autocorrelation
                signal = signal - np.mean(signal)
                autocorr = np.correlate(signal, signal, mode='full')
                autocorr = autocorr[len(autocorr)//2:]
                autocorr = autocorr / (autocorr[0] + 1e-10)

                peaks = []
                for i in range(2, min(max_period * 2, len(autocorr) - 1)):
                    if autocorr[i] > autocorr[i-1] and autocorr[i] > autocorr[i+1] and autocorr[i] > 0.1:
                        peaks.append((i, autocorr[i]))

                if peaks:
                    return peaks[0][0], peaks[0][1]
                return 0, 0

            row_period, row_peak = detect_periodicity(row_vars)
            col_period, col_peak = detect_periodicity(col_vars)

            if row_peak > 0.3 or col_peak > 0.3:
                resample_data['upscaling_detected'] = True
                resample_data['resample_confidence'] = round(max(row_peak, col_peak) * 100, 1)

                if row_period > 0 and col_period > 0:
                    resample_data['interpolation_type'] = "Bilinear/Bicubic upscaling"
                    resample_data['details'].append(f"Periodic interpolation artifacts detected (period: {row_period}x{col_period})")
                    resample_data['resample_score'] -= 25
                else:
                    resample_data['details'].append("Possible resampling artifacts detected")
                    resample_data['resample_score'] -= 15
            else:
                resample_data['details'].append("No resampling artifacts detected")
                resample_data['resample_score'] += 15

            # Check for JPEG block alignment (misaligned blocks suggest crop/resample)
            h, w = img_gray.shape
            h8, w8 = (h // 8) * 8, (w // 8) * 8

            if h8 < h or w8 < w:
                resample_data['details'].append("Image dimensions not aligned to 8×8 blocks — possible crop")
                resample_data['resample_score'] -= 10

        except Exception as e:
            resample_data['details'].append(f"Resampling detection error: {str(e)}")

        resample_data['resample_score'] = max(0, min(100, resample_data['resample_score']))
        self.results['resampling'] = resample_data

    def _analyze_local_patches(self, img_rgb, img_gray):
        """Analyze image in local patches for inconsistencies."""
        patch_data = {
            'anomaly_score': None,
            'suspicious_regions': 0,
            'patch_score': 50,
            'details': [],
            'heatmap': None
        }

        try:
            patch_size = 64
            stride = 32
            h, w = img_gray.shape

            patch_scores = []
            patch_positions = []

            for i in range(0, h - patch_size, stride):
                for j in range(0, w - patch_size, stride):
                    patch = img_gray[i:i+patch_size, j:j+patch_size]
                    patch_rgb = img_rgb[i:i+patch_size, j:j+patch_size]

                    # Compute patch features
                    # 1. Noise variance
                    denoised = cv2.GaussianBlur(patch.astype(float), (3,3), 0.5)
                    noise_var = np.var(patch.astype(float) - denoised)

                    # 2. Local frequency energy
                    f_transform = np.fft.fft2(patch.astype(float))
                    magnitude = np.abs(f_transform)
                    high_freq = np.sum(magnitude[patch_size//2:, :]) + np.sum(magnitude[:, patch_size//2:])
                    total_freq = np.sum(magnitude)
                    hf_ratio = high_freq / (total_freq + 1e-10)

                    # 3. Texture (Laplacian variance)
                    lap_var = np.var(cv2.Laplacian(patch, cv2.CV_32F))

                    # 4. Color variance
                    color_var = np.var(patch_rgb[:,:,0]) + np.var(patch_rgb[:,:,1]) + np.var(patch_rgb[:,:,2])

                    # Combine into anomaly score (deviation from expected natural range)
                    score = 0

                    # Very low noise is suspicious
                    if noise_var < 1:
                        score += 30
                    elif noise_var > 50:
                        score += 10

                    # Very low high-freq is suspicious
                    if hf_ratio < 0.1:
                        score += 20

                    # Very low texture is suspicious
                    if lap_var < 10:
                        score += 15

                    patch_scores.append(score)
                    patch_positions.append((i, j))

            if patch_scores:
                patch_scores = np.array(patch_scores)
                patch_data['anomaly_score'] = round(float(np.mean(patch_scores)), 2)
                patch_data['suspicious_regions'] = int(np.sum(patch_scores > 50))

                # Create heatmap
                heatmap_h = (h - patch_size) // stride + 1
                heatmap_w = (w - patch_size) // stride + 1
                heatmap = patch_scores.reshape(heatmap_h, heatmap_w)

                # Normalize and resize to image size
                heatmap_norm = (heatmap - heatmap.min()) / (heatmap.max() - heatmap.min() + 1e-10)
                heatmap_resized = cv2.resize((heatmap_norm * 255).astype(np.uint8), (w, h))
                heatmap_color = cv2.applyColorMap(heatmap_resized, cv2.COLORMAP_HOT)

                # Overlay on original
                overlay = cv2.addWeighted(img_rgb[:,:,::-1], 0.5, heatmap_color, 0.5, 0)
                patch_data['heatmap'] = self._img_to_base64(overlay)

                # Score
                suspicious_ratio = patch_data['suspicious_regions'] / len(patch_scores)
                if suspicious_ratio > 0.3:
                    patch_data['details'].append(f"{patch_data['suspicious_regions']} suspicious patches detected ({suspicious_ratio*100:.1f}%)")
                    patch_data['patch_score'] -= 25
                elif suspicious_ratio > 0.1:
                    patch_data['details'].append(f"Some patch inconsistencies found")
                    patch_data['patch_score'] -= 10
                else:
                    patch_data['details'].append("Patches appear consistent")
                    patch_data['patch_score'] += 15

        except Exception as e:
            patch_data['details'].append(f"Local patch analysis error: {str(e)}")

        patch_data['patch_score'] = max(0, min(100, patch_data['patch_score']))
        self.results['local_patches'] = patch_data

    def _analyze_texture(self, img_gray):
        """Analyze texture characteristics."""
        texture_data = {
            'lbp_uniformity': None,
            'glcm_contrast': None,
            'glcm_homogeneity': None,
            'glcm_energy': None,
            'glcm_entropy': None,
            'texture_score': 50,
            'details': []
        }

        try:
            # Local Binary Patterns (manual implementation)
            def manual_lbp(image, P=8, R=1):
                h, w = image.shape
                lbp = np.zeros((h - 2*R, w - 2*R), dtype=np.uint8)
                angles = np.linspace(0, 2*np.pi, P, endpoint=False)
                center = image[R:h-R, R:w-R].astype(np.float32)
                for i, angle in enumerate(angles):
                    dy, dx = int(round(R * np.sin(angle))), int(round(R * np.cos(angle)))
                    neighbor = image[R+dy:h-R+dy, R+dx:w-R+dx].astype(np.float32)
                    lbp += ((neighbor >= center).astype(np.uint8) << i)
                return lbp

            lbp = manual_lbp(img_gray, P=8, R=1)
            # Uniform pattern counting
            lbp_hist = np.zeros(10)
            for val in lbp.flatten():
                # Count transitions (0->1 or 1->0) in binary
                transitions = 0
                for i in range(8):
                    bit1 = (val >> i) & 1
                    bit2 = (val >> ((i+1) % 8)) & 1
                    if bit1 != bit2:
                        transitions += 1
                if transitions <= 2:
                    lbp_hist[min(transitions, 9)] += 1
                else:
                    lbp_hist[9] += 1
            lbp_hist = lbp_hist / (np.sum(lbp_hist) + 1e-10)

            # Uniformity: real textures have varied LBP patterns
            texture_data['lbp_uniformity'] = round(float(np.max(lbp_hist)), 4)

            if texture_data['lbp_uniformity'] > 0.5:
                texture_data['details'].append("LBP patterns too uniform — possible artificial texture")
                texture_data['texture_score'] -= 15
            else:
                texture_data['details'].append("LBP patterns show natural variation")
                texture_data['texture_score'] += 10

            # Texture features via OpenCV (no skimage)
            small = cv2.resize(img_gray, (256, 256))

            # Local variance as texture measure
            local_var = cv2.Laplacian(small, cv2.CV_32F)
            texture_data['glcm_contrast'] = round(float(np.var(local_var)), 4)

            # Homogeneity via gradient magnitude
            sobelx = cv2.Sobel(small, cv2.CV_32F, 1, 0, ksize=3)
            sobely = cv2.Sobel(small, cv2.CV_32F, 0, 1, ksize=3)
            grad_mag = np.sqrt(sobelx**2 + sobely**2)
            texture_data['glcm_homogeneity'] = round(float(1.0 / (1.0 + np.mean(grad_mag))), 4)

            # Energy via histogram concentration
            hist = cv2.calcHist([small], [0], None, [256], [0, 256])
            hist = hist / (np.sum(hist) + 1e-10)
            texture_data['glcm_energy'] = round(float(np.sum(hist**2)), 4)

            # Entropy
            texture_data['glcm_entropy'] = round(float(-np.sum(hist * np.log(hist + 1e-10))), 4)

            if texture_data['glcm_energy'] > 0.3:
                texture_data['details'].append("High texture energy — texture too ordered, possible AI")
                texture_data['texture_score'] -= 10
            else:
                texture_data['details'].append("Texture energy within natural range")
                texture_data['texture_score'] += 5

        except Exception as e:
            texture_data['details'].append(f"Texture analysis error: {str(e)}")

        texture_data['texture_score'] = max(0, min(100, texture_data['texture_score']))
        self.results['texture_analysis'] = texture_data

    def _analyze_edges(self, img_gray):
        """Analyze edge characteristics."""
        edge_data = {
            'edge_density': None,
            'edge_sharpness': None,
            'edge_continuity': None,
            'gradient_consistency': None,
            'edge_score': 50,
            'details': []
        }

        try:
            # Canny edges
            edges = cv2.Canny(img_gray, 50, 150)
            edge_data['edge_density'] = round(float(np.sum(edges > 0) / edges.size * 100), 4)

            # Edge sharpness using Sobel
            sobelx = cv2.Sobel(img_gray, cv2.CV_32F, 1, 0, ksize=3)
            sobely = cv2.Sobel(img_gray, cv2.CV_32F, 0, 1, ksize=3)
            gradient_mag = np.sqrt(sobelx**2 + sobely**2)

            edge_data['edge_sharpness'] = round(float(np.mean(gradient_mag)), 2)

            # Edge continuity: real edges have natural variation
            # AI edges can be unnaturally smooth or sharp
            edge_mask = edges > 0
            if np.sum(edge_mask) > 100:
                edge_gradients = gradient_mag[edge_mask]
                edge_data['gradient_consistency'] = round(float(np.std(edge_gradients) / (np.mean(edge_gradients) + 1e-10)), 4)

                if edge_data['gradient_consistency'] < 0.3:
                    edge_data['details'].append("Edge gradients too consistent — possible AI generation")
                    edge_data['edge_score'] -= 15
                else:
                    edge_data['details'].append("Edge gradients show natural variation")
                    edge_data['edge_score'] += 10

            # Check for unnaturally sharp edges (common in AI)
            if edge_data['edge_sharpness'] > 50:
                edge_data['details'].append("Very high edge sharpness — possible over-sharpening or AI")
                edge_data['edge_score'] -= 10

        except Exception as e:
            edge_data['details'].append(f"Edge analysis error: {str(e)}")

        edge_data['edge_score'] = max(0, min(100, edge_data['edge_score']))
        self.results['edge_analysis'] = edge_data

    def _compute_overall_score(self):
        """Compute overall AI vs Real score."""
        scores = {
            'metadata': self.results['metadata'].get('score', 50),
            'jpeg': self.results['jpeg_analysis'].get('compression_score', 50),
            'noise': self.results['noise_analysis'].get('noise_score', 50),
            'frequency': self.results['frequency_analysis'].get('freq_score', 50),
            'color': self.results['color_analysis'].get('color_score', 50),
            'resampling': self.results['resampling'].get('resample_score', 50),
            'patches': self.results['local_patches'].get('patch_score', 50),
            'texture': self.results['texture_analysis'].get('texture_score', 50),
            'edges': self.results['edge_analysis'].get('edge_score', 50)
        }

        # Weighted average — noise and frequency are strongest discriminators
        weights = {
            'metadata': 0.10,
            'jpeg': 0.10,
            'noise': 0.20,
            'frequency': 0.15,
            'color': 0.08,
            'resampling': 0.10,
            'patches': 0.12,
            'texture': 0.08,
            'edges': 0.07
        }

        weighted_score = sum(scores[k] * weights[k] for k in scores)
        self.results['overall_score'] = round(weighted_score, 2)

        # Verdict
        if weighted_score >= 65:
            self.results['verdict'] = "LIKELY REAL PHOTOGRAPH"
            self.results['confidence'] = round(min(100, weighted_score), 1)
        elif weighted_score <= 35:
            self.results['verdict'] = "LIKELY AI-GENERATED"
            self.results['confidence'] = round(min(100, 100 - weighted_score), 1)
        else:
            self.results['verdict'] = "UNCERTAIN"
            self.results['confidence'] = round(50 - abs(weighted_score - 50), 1)

        self.results['score_breakdown'] = scores

    def _img_to_base64(self, img):
        """Convert OpenCV image to base64 string."""
        _, buffer = cv2.imencode('.png', img)
        return base64.b64encode(buffer).decode('utf-8')

    def _create_histogram_image(self, r, g, b):
        """Create RGB histogram image as base64 using PIL (no matplotlib)."""
        from PIL import Image, ImageDraw, ImageFont

        W, H = 800, 400
        img = Image.new('RGB', (W, H), (18, 18, 26))
        draw = ImageDraw.Draw(img)

        # Margins
        margin_left = 60
        margin_bottom = 50
        margin_top = 40
        margin_right = 20

        chart_w = W - margin_left - margin_right
        chart_h = H - margin_top - margin_bottom

        # Compute histograms
        bins = 50
        r_hist, _ = np.histogram(r.flatten(), bins=bins, range=(0, 256), density=True)
        g_hist, _ = np.histogram(g.flatten(), bins=bins, range=(0, 256), density=True)
        b_hist, _ = np.histogram(b.flatten(), bins=bins, range=(0, 256), density=True)

        max_val = max(r_hist.max(), g_hist.max(), b_hist.max())
        if max_val > 0:
            r_hist = r_hist / max_val
            g_hist = g_hist / max_val
            b_hist = b_hist / max_val

        bin_width = chart_w / bins

        # Draw axes
        draw.line([(margin_left, margin_top), (margin_left, H - margin_bottom)], fill=(60, 60, 80), width=1)
        draw.line([(margin_left, H - margin_bottom), (W - margin_right, H - margin_bottom)], fill=(60, 60, 80), width=1)

        # Draw histogram bars
        for i in range(bins):
            x = margin_left + int(i * bin_width)
            next_x = margin_left + int((i + 1) * bin_width)
            bw = max(1, next_x - x - 1)

            # R
            rh = int(r_hist[i] * chart_h)
            if rh > 0:
                draw.rectangle([x, H - margin_bottom - rh, x + bw, H - margin_bottom], 
                              fill=(200, 50, 50, 128), outline=None)

            # G
            gh = int(g_hist[i] * chart_h)
            if gh > 0:
                draw.rectangle([x, H - margin_bottom - gh, x + bw, H - margin_bottom], 
                              fill=(50, 200, 50, 128), outline=None)

            # B
            bh = int(b_hist[i] * chart_h)
            if bh > 0:
                draw.rectangle([x, H - margin_bottom - bh, x + bw, H - margin_bottom], 
                              fill=(50, 50, 200, 128), outline=None)

        # Title
        try:
            font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 16)
            small_font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 12)
        except:
            font = ImageFont.load_default()
            small_font = font

        draw.text((W//2 - 100, 10), "RGB Color Histogram", fill=(200, 200, 220), font=font)
        draw.text((margin_left, H - 35), "0", fill=(150, 150, 170), font=small_font)
        draw.text((W - margin_right - 30, H - 35), "255", fill=(150, 150, 170), font=small_font)
        draw.text((10, H//2), "Density", fill=(150, 150, 170), font=small_font)

        # Legend
        draw.rectangle([W - 120, margin_top, W - 100, margin_top + 12], fill=(200, 50, 50))
        draw.text((W - 95, margin_top), "R", fill=(200, 200, 220), font=small_font)
        draw.rectangle([W - 70, margin_top, W - 50, margin_top + 12], fill=(50, 200, 50))
        draw.text((W - 45, margin_top), "G", fill=(200, 200, 220), font=small_font)
        draw.rectangle([W - 30, margin_top, W - 10, margin_top + 12], fill=(50, 50, 200))
        draw.text((W - 5, margin_top), "B", fill=(200, 200, 220), font=small_font)

        buf = io.BytesIO()
        img.save(buf, format='PNG')
        buf.seek(0)
        return base64.b64encode(buf.read()).decode('utf-8')
