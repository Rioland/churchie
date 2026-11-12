// Churchie website: menu, projector demo, download picker, docs contents.
(function () {
  "use strict";

  // Every download link is /get/<file name>. netlify.toml sends that on to wherever the
  // files are kept, so the website never has to know (or change) where that is.
  var DOWNLOADS = "/get/";
  var FILES = {
    windows: { file: "Churchie-Setup-win-x64.exe", label: "Download for Windows" },
    "mac-arm": { file: "Churchie-osx-arm64.pkg", label: "Download for Mac (Apple Silicon)" },
    "mac-intel": { file: "Churchie-osx-x64.pkg", label: "Download for Mac (Intel)" },
    linux: { file: "Churchie-linux-x64.tar.gz", label: "Download for Linux" },
  };
  var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  // ---------- Mobile menu ----------
  var toggle = document.querySelector(".nav-toggle");
  var nav = document.getElementById("site-nav");
  if (toggle && nav) {
    toggle.addEventListener("click", function () {
      var open = nav.classList.toggle("open");
      toggle.setAttribute("aria-expanded", open ? "true" : "false");
    });
  }

  // ---------- Projector demo (home page) ----------
  var screen = document.querySelector("[data-demo]");
  if (screen) {
    var slides = Array.prototype.slice.call(screen.querySelectorAll(".slide"));
    var now = document.querySelector("[data-demo-now]");
    var index = 0;
    var timer = null;

    var show = function (i) {
      slides[index].classList.remove("on");
      index = (i + slides.length) % slides.length;
      slides[index].classList.add("on");
      if (now) now.innerHTML = slides[index].getAttribute("data-label");
    };
    var start = function () {
      if (reduceMotion) return; // no automatic changes for people who prefer less motion
      stop();
      timer = window.setInterval(function () { show(index + 1); }, 5200);
    };
    var stop = function () { if (timer) window.clearInterval(timer); timer = null; };

    document.querySelectorAll("[data-demo-next]").forEach(function (b) {
      b.addEventListener("click", function () { show(index + 1); start(); });
    });
    document.querySelectorAll("[data-demo-prev]").forEach(function (b) {
      b.addEventListener("click", function () { show(index - 1); start(); });
    });
    document.addEventListener("visibilitychange", function () { document.hidden ? stop() : start(); });
    show(0);
    start();
  }

  // ---------- Which computer is this? ----------
  function detectPlatform() {
    var ua = navigator.userAgent || "";
    var platform = (navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || "";
    if (/iPhone|iPad|iPod|Android/i.test(ua)) return "phone";
    if (/Win/i.test(platform) || /Windows/i.test(ua)) return "windows";
    if (/Mac/i.test(platform) || /Macintosh/i.test(ua)) return "mac";
    if (/Linux|X11/i.test(platform) || /Linux/i.test(ua)) return "linux";
    return "other";
  }

  // Browsers report Intel even on Apple Silicon Macs; Chromium can tell us the truth.
  function macArchitecture() {
    if (navigator.userAgentData && navigator.userAgentData.getHighEntropyValues) {
      return navigator.userAgentData.getHighEntropyValues(["architecture"]).then(function (v) {
        return v.architecture === "arm" ? "mac-arm" : v.architecture === "x86" ? "mac-intel" : "mac-arm";
      }).catch(function () { return "mac-arm"; });
    }
    // Most Macs sold since 2021 are Apple Silicon, so that's the safer guess.
    return Promise.resolve("mac-arm");
  }

  function setButton(button, key) {
    var info = FILES[key];
    if (!info) return;
    button.href = DOWNLOADS + info.file;
    var label = button.querySelector("[data-label]");
    if (label) label.textContent = info.label;
  }

  var platform = detectPlatform();
  var autoButtons = document.querySelectorAll("[data-download='auto']");
  var platformCards = document.querySelectorAll("[data-platform]");

  var choose = function (key) {
    autoButtons.forEach(function (b) { setButton(b, key); });
    var family = key.indexOf("mac") === 0 ? "mac" : key;
    platformCards.forEach(function (card) {
      card.classList.toggle("match", card.getAttribute("data-platform") === family);
    });
    document.querySelectorAll("[data-detected]").forEach(function (el) {
      var names = { windows: "Windows", "mac-arm": "a Mac", "mac-intel": "a Mac", linux: "Linux" };
      el.textContent = names[key] ? "It looks like you're using " + names[key] + "." : "";
    });
  };

  if (platform === "windows" || platform === "linux") choose(platform);
  else if (platform === "mac") macArchitecture().then(choose);
  else {
    // Phone or unknown: send the main button to the download page instead.
    autoButtons.forEach(function (b) {
      b.href = "download.html";
      var label = b.querySelector("[data-label]");
      if (label) label.textContent = "Choose a download";
    });
    document.querySelectorAll("[data-detected]").forEach(function (el) {
      el.textContent = platform === "phone"
        ? "Churchie runs on computers, not phones. Open this page on the computer you'll use for church."
        : "";
    });
  }

  // ---------- "Your download has started" ----------
  // The browser saves the file without leaving the page, so nothing on screen changes.
  // A short note confirms it worked and points to the install steps.
  var fileOf = function (a) { return a.getAttribute("href").split("/").pop(); };
  var INSTALL_STEPS = { exe: "#windows", zip: "#windows", pkg: "#mac", gz: "#linux" };
  var toast = null, toastTimer = null;
  document.addEventListener("click", function (e) {
    var a = e.target.closest ? e.target.closest("a[href^='" + DOWNLOADS + "']") : null;
    if (!a) return;
    var name = fileOf(a);
    if (name === "SHA256SUMS.txt") return;
    var ext = name.split(".").pop();
    var steps = "download.html" + (/osx/.test(name) ? "#mac" : (INSTALL_STEPS[ext] || ""));
    if (!toast) {
      toast = document.createElement("div");
      toast.className = "toast";
      toast.setAttribute("role", "status");
      document.body.appendChild(toast);
    }
    toast.innerHTML = "";
    var text = document.createElement("p");
    text.innerHTML = "<strong>Your download has started.</strong> ";
    text.appendChild(document.createTextNode(name + " is being saved to your Downloads folder."));
    var links = document.createElement("p");
    var again = document.createElement("a"); again.href = a.href; again.textContent = "Didn't start? Try again";
    var how = document.createElement("a"); how.href = steps; how.textContent = "How to install";
    var close = document.createElement("button"); close.type = "button"; close.className = "toast-close";
    close.setAttribute("aria-label", "Close"); close.textContent = "\u00d7";
    close.addEventListener("click", function () { toast.hidden = true; });
    links.appendChild(again); links.appendChild(document.createTextNode(" · ")); links.appendChild(how);
    toast.appendChild(text); toast.appendChild(links); toast.appendChild(close);
    toast.hidden = false;
    window.clearTimeout(toastTimer);
    toastTimer = window.setTimeout(function () { toast.hidden = true; }, 15000);
  });

  // ---------- Docs: highlight the section you're reading ----------
  var tocLinks = document.querySelectorAll(".toc a[href^='#']");
  if (tocLinks.length && "IntersectionObserver" in window) {
    var byId = {};
    tocLinks.forEach(function (a) { byId[a.getAttribute("href").slice(1)] = a; });
    tocLinks[0].classList.add("active"); // the first section is on screen when the page opens
    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        tocLinks.forEach(function (a) { a.classList.remove("active"); });
        var link = byId[entry.target.id];
        if (link) link.classList.add("active");
      });
    }, { rootMargin: "-90px 0px -70% 0px" });
    Object.keys(byId).forEach(function (id) {
      var section = document.getElementById(id);
      if (section) observer.observe(section);
    });
  }
  var jump = document.querySelector("[data-toc-jump]");
  if (jump) {
    jump.addEventListener("change", function () { if (jump.value) location.hash = jump.value; });
  }

  // ---------- Contact form: hand the message to WhatsApp or email ----------
  // A static website can't send messages by itself, so the form writes the message
  // into WhatsApp (or the visitor's email app) and the visitor presses Send there.
  var contact = document.querySelector("[data-contact]");
  if (contact) {
    var PHONE = "2348149916721";          // wa.me wants the country code and number, no "+" or spaces
    var EMAIL = "riotech2222@gmail.com";
    var field = function (id) { return document.getElementById(id); };
    var value = function (id) { return field(id).value.trim(); };
    var status = contact.querySelector("[data-contact-status]");

    var say = function (text) { status.textContent = text; status.hidden = false; };

    // The message exactly as it will appear in WhatsApp or the email.
    var compose = function () {
      var church = value("c-church");
      return "Hello, I'm " + value("c-name") + (church ? " from " + church : "") + ".\n" +
             "About: " + value("c-topic") + "\n\n" +
             value("c-message") + "\n\n" +
             "(Sent from the Churchie website)";
    };

    // "required" alone accepts a box full of spaces, so check the trimmed text too.
    var ready = function () {
      ["c-name", "c-message"].forEach(function (id) {
        field(id).setCustomValidity(value(id) ? "" : "Please fill this in.");
      });
      return contact.reportValidity();
    };
    ["c-name", "c-message"].forEach(function (id) {
      field(id).addEventListener("input", function () { field(id).setCustomValidity(""); });
    });

    contact.addEventListener("submit", function (e) {
      e.preventDefault();
      if (!ready()) return;
      var url = "https://wa.me/" + PHONE + "?text=" + encodeURIComponent(compose());
      var tab = window.open(url, "_blank");
      if (tab) tab.opener = null; else location.href = url; // pop-up blocked: open it here instead
      say("WhatsApp should now be open with your message. Press Send in WhatsApp to deliver it.");
    });

    contact.querySelector("[data-contact-email]").addEventListener("click", function () {
      if (!ready()) return;
      location.href = "mailto:" + EMAIL +
        "?subject=" + encodeURIComponent("Churchie: " + value("c-topic")) +
        "&body=" + encodeURIComponent(compose());
      say("Your email app should now be open with the message. Press Send there. If nothing opened, email " + EMAIL + " directly.");
    });
  }
})();
