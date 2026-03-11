/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  content: [
    './**/*.razor',
    './**/*.cshtml',
    './wwwroot/index.html',
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'monospace'],
      },
      colors: {
        'guardian-ink': '#0F172A',
        'guardian-panel': '#FFFFFF',
        'guardian-canvas': '#F3F5F7',
        'guardian-line': '#D7DEE7',
        'guardian-primary': '#245DFF',
        'guardian-success': '#1F9D55',
        'guardian-warning': '#C98300',
        'guardian-danger': '#C93B3B',
      },
      boxShadow: {
        panel: '0 20px 60px rgba(15, 23, 42, 0.08)',
        float: '0 28px 80px rgba(15, 23, 42, 0.16)',
      },
      gridTemplateColumns: {
        guardian: 'minmax(0, 1.5fr) minmax(320px, 0.9fr)',
      },
    },
  },
  plugins: [
    require('@tailwindcss/forms'),
    require('@tailwindcss/typography'),
    require('@tailwindcss/container-queries'),
  ],
};
