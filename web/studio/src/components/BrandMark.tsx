import { Link } from 'react-router-dom';

type BrandMarkProps = {
  /** Full stacked logo (icon + wordmark + tagline) vs icon-only mark. */
  variant?: 'full' | 'icon';
  /** Wrap in a home link. */
  to?: string;
  className?: string;
  imgClassName?: string;
  alt?: string;
};

const SRC = {
  full: '/brand/logo-full.jpg',
  icon: '/brand/icon.png',
} as const;

export function BrandMark({
  variant = 'full',
  to,
  className,
  imgClassName,
  alt = 'DevWeave',
}: BrandMarkProps) {
  const fallbackClass =
    variant === 'full'
      ? 'block h-auto w-full max-w-md object-contain'
      : 'block h-10 w-10 object-contain bg-white/95';

  const img = (
    <img
      src={SRC[variant]}
      alt={alt}
      className={imgClassName ?? fallbackClass}
      decoding="async"
      loading="eager"
    />
  );

  if (!to) {
    return <div className={className}>{img}</div>;
  }

  return (
    <Link to={to} className={className ?? 'inline-flex shrink-0 items-center hover:opacity-90'} aria-label="DevWeave home">
      {img}
    </Link>
  );
}
