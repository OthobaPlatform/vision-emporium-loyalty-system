interface LogoProps {
  className?: string;
  size?: 'sm' | 'md' | 'lg';
  showText?: boolean;
}

/**
 * Vision Emporium logo component.
 * Renders the brand logo with the iconic eye "O" in VISION and red EMPORIUM text.
 */
export function VisionEmporiumLogo({ className = '', size = 'md', showText = true }: LogoProps) {
  const sizes = {
    sm: { width: 120, height: 50 },
    md: { width: 200, height: 80 },
    lg: { width: 300, height: 120 },
  };

  const { width, height } = sizes[size];

  return (
    <svg
      viewBox="0 0 300 120"
      width={width}
      height={height}
      className={className}
      aria-label="Vision Emporium logo"
      role="img"
    >
      {/* VISION text */}
      <text
        x="30"
        y="65"
        fontFamily="Arial Black, Arial, sans-serif"
        fontSize="48"
        fontWeight="900"
        fill="#1a1a1a"
        letterSpacing="-1"
      >
        VISI
      </text>

      {/* Eye "O" - the iconic red/black eye */}
      <circle cx="185" cy="52" r="20" fill="#E31E24" />
      <circle cx="185" cy="52" r="12" fill="#1a1a1a" />
      <circle cx="185" cy="52" r="6" fill="#E31E24" opacity="0.8" />
      <circle cx="183" cy="49" r="3" fill="#ffffff" />

      {/* N after the eye */}
      <text
        x="205"
        y="65"
        fontFamily="Arial Black, Arial, sans-serif"
        fontSize="48"
        fontWeight="900"
        fill="#1a1a1a"
        letterSpacing="-1"
      >
        N
      </text>

      {/* ® symbol */}
      <text
        x="240"
        y="35"
        fontFamily="Arial, sans-serif"
        fontSize="12"
        fill="#1a1a1a"
      >
        ®
      </text>

      {showText && (
        /* EMPORIUM text in red */
        <text
          x="75"
          y="100"
          fontFamily="Arial Black, Arial, sans-serif"
          fontSize="32"
          fontWeight="900"
          fill="#E31E24"
          letterSpacing="2"
        >
          EMPORIUM
        </text>
      )}
    </svg>
  );
}

/**
 * Small icon-only version of the logo (just the eye).
 */
export function VisionEmporiumIcon({ className = '' }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 40 40"
      width="32"
      height="32"
      className={className}
      aria-label="Vision Emporium"
      role="img"
    >
      <circle cx="20" cy="20" r="18" fill="#E31E24" />
      <circle cx="20" cy="20" r="11" fill="#1a1a1a" />
      <circle cx="20" cy="20" r="5" fill="#E31E24" opacity="0.8" />
      <circle cx="18" cy="17" r="2.5" fill="#ffffff" />
    </svg>
  );
}
