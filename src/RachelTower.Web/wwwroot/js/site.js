// Small progressive enhancements. The site works without JavaScript.
(() => {
  "use strict";

  // Mobile menu
  const toggle = document.querySelector("[data-nav-toggle]");
  const mobileNav = document.querySelector("[data-mobile-nav]");
  if (toggle && mobileNav) {
    toggle.addEventListener("click", () => {
      const open = toggle.getAttribute("aria-expanded") === "true";
      toggle.setAttribute("aria-expanded", String(!open));
      mobileNav.classList.toggle("hidden", open);
    });
  }

  // Home page slideshow: cross-fade every 6 seconds, unless the visitor prefers reduced motion.
  const slides = [...document.querySelectorAll("[data-hero-slide]")];
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  if (slides.length > 1 && !reduceMotion) {
    let current = 0;
    setInterval(() => {
      if (document.hidden) return;
      slides[current].classList.remove("is-active");
      current = (current + 1) % slides.length;
      slides[current].loading = "eager";
      slides[current].classList.add("is-active");
    }, 6000);
  }

  // Booking form: departure must be after arrival.
  const checkIn = document.querySelector("[data-checkin]");
  const checkOut = document.querySelector("[data-checkout]");
  if (checkIn && checkOut) {
    const today = new Date().toISOString().slice(0, 10);
    checkIn.min = today;
    const sync = () => {
      if (!checkIn.value) return;
      const next = new Date(checkIn.value);
      next.setDate(next.getDate() + 1);
      const min = next.toISOString().slice(0, 10);
      checkOut.min = min;
      if (!checkOut.value || checkOut.value < min) checkOut.value = min;
    };
    checkIn.addEventListener("change", sync);
    sync();
  }

  // Photo galleries: open the full-size photo in a lightbox, with keyboard and swipe support.
  document.querySelectorAll("[data-gallery]").forEach((gallery) => {
    const dialog = gallery.nextElementSibling;
    if (!(dialog instanceof HTMLDialogElement)) return;
    const items = [...gallery.querySelectorAll("[data-gallery-item]")];
    const img = dialog.querySelector("[data-lightbox-img]");
    const caption = dialog.querySelector("[data-lightbox-caption]");
    let index = 0;

    const show = (i) => {
      index = (i + items.length) % items.length;
      const thumb = items[index].querySelector("img");
      img.src = items[index].href;
      img.alt = thumb.alt;
      caption.textContent = thumb.alt;
    };

    items.forEach((item, i) =>
      item.addEventListener("click", (e) => {
        e.preventDefault();
        show(i);
        dialog.showModal();
      })
    );

    dialog.querySelector("[data-lightbox-close]").addEventListener("click", () => dialog.close());
    dialog.querySelector("[data-lightbox-prev]").addEventListener("click", () => show(index - 1));
    dialog.querySelector("[data-lightbox-next]").addEventListener("click", () => show(index + 1));
    dialog.addEventListener("click", (e) => { if (e.target === dialog || e.target.tagName === "FIGURE") dialog.close(); });
    dialog.addEventListener("keydown", (e) => {
      if (e.key === "ArrowLeft") show(index - 1);
      if (e.key === "ArrowRight") show(index + 1);
    });

    let startX = null;
    dialog.addEventListener("touchstart", (e) => { startX = e.touches[0].clientX; }, { passive: true });
    dialog.addEventListener("touchend", (e) => {
      if (startX === null) return;
      const dx = e.changedTouches[0].clientX - startX;
      if (Math.abs(dx) > 50) show(index + (dx < 0 ? 1 : -1));
      startX = null;
    });
  });
})();
