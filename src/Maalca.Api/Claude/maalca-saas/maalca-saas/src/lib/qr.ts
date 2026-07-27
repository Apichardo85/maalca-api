// src/lib/qr.ts
import QRCode from 'qrcode';

/**
 * Generate a PNG buffer for a URL.
 * Used by /api/qr/[slug] to serve a QR image inline.
 */
export async function generateQRPng(url: string, size = 512): Promise<Buffer> {
  return QRCode.toBuffer(url, {
    type: 'png',
    width: size,
    margin: 2,
    color: {
      dark: '#000000',
      light: '#FFFFFF',
    },
    errorCorrectionLevel: 'M',
  });
}

export async function generateQRDataURL(url: string, size = 256): Promise<string> {
  return QRCode.toDataURL(url, {
    width: size,
    margin: 2,
    errorCorrectionLevel: 'M',
  });
}
