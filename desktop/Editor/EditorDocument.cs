namespace KitaplikEditoru.Editor;

public static class EditorDocument
{
    public const string Html = """
        <!doctype html>
        <html lang="tr">
        <head>
          <meta charset="utf-8">
          <meta http-equiv="X-UA-Compatible" content="IE=edge">
          <base id="contentBase" href="about:blank">
          <style>
            :root { color-scheme: light; font-family: Georgia, 'Times New Roman', serif; color: #242a31; background: #e8ebef; }
            * { box-sizing: border-box; }
            body { margin: 0; min-height: 100vh; padding: 32px 16px; }
            #page { width: min(100%, 820px); min-height: calc(100vh - 64px); margin: 0 auto; padding: 72px 82px; background: #fff; box-shadow: 0 2px 18px #17202b1c; line-height: 1.7; font-size: 16px; outline: none; overflow-wrap: anywhere; }
            #page:empty::before { content: __START_WRITING__; color: #a0a8b2; pointer-events: none; }
            a { color: #176b9b; text-decoration: underline; }
            img { max-width: 100%; height: auto; }
            table { border-collapse: collapse; max-width: 100%; }
            td, th { border: 1px solid #8a929d; padding: 5px 8px; }
            html[data-theme='dark'] { color-scheme: dark; color: #e5e9ed; background: #171b1f; }
            html[data-theme='dark'] body { background: #171b1f; }
            html[data-theme='dark'] #page { color: #e5e9ed; background: #252b31; box-shadow: 0 2px 18px #0008; }
            html[data-theme='dark'] a { color: #8bc8f0; }
            @media (max-width: 700px) { body { padding: 0; } #page { min-height: 100vh; padding: 32px 24px; } }
          </style>
        </head>
        <body>
          <article id="page" contenteditable="true" spellcheck="true" lang="tr"></article>
          <script>
            const page = document.getElementById('page');
            const voidTags = new Set(['area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'param', 'source', 'track', 'wbr']);
            function escapeXml(value) { return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\"/g, '&quot;').replace(/'/g, '&apos;'); }
            function serialize(node) {
              if (node.nodeType === Node.TEXT_NODE) return escapeXml(node.nodeValue || '');
              if (node.nodeType === Node.COMMENT_NODE) return '<!--' + (node.nodeValue || '').replace(/--/g, '- -') + '-->';
              if (node.nodeType !== Node.ELEMENT_NODE) return '';
              const tag = node.localName.toLowerCase();
              const namespaceUris = { epub: 'http://www.idpf.org/2007/ops', xlink: 'http://www.w3.org/1999/xlink' };
              const namespaceAttrs = Object.entries(namespaceUris)
                .filter(([prefix]) => Array.from(node.attributes).some(attr => attr.name.toLowerCase().startsWith(prefix + ':')) && !node.hasAttribute('xmlns:' + prefix))
                .map(([prefix, uri]) => ' xmlns:' + prefix + '=\"' + uri + '\"').join('');
              const attrs = Array.from(node.attributes).map(attr => ' ' + attr.name.toLowerCase() + '=\"' + escapeXml(attr.value) + '\"').join('');
              if (voidTags.has(tag)) return '<' + tag + namespaceAttrs + attrs + ' />';
              return '<' + tag + namespaceAttrs + attrs + '>' + Array.from(node.childNodes).map(serialize).join('') + '</' + tag + '>';
            }
            function serializeContent() { return Array.from(page.childNodes).map(serialize).join(''); }
            let currentSectionId = null;
            let inputTimer;
            function flushContent() {
              clearTimeout(inputTimer);
              if (currentSectionId) chrome.webview.postMessage(JSON.stringify({ type: 'content', sectionId: currentSectionId, content: serializeContent() }));
            }
            function setContent(markup, sectionId) {
              currentSectionId = sectionId;
              page.innerHTML = markup;
              page.scrollTop = 0;
            }
            function setBasePath(path) { document.getElementById('contentBase').href = path; }
            function setTheme(dark) { document.documentElement.dataset.theme = dark ? 'dark' : 'light'; }
            function setStyles(paths) {
              document.querySelectorAll('link[data-book-style]').forEach(link => link.remove());
              for (const path of paths) {
                const link = document.createElement('link');
                link.rel = 'stylesheet';
                link.href = path;
                link.dataset.bookStyle = 'true';
                document.head.appendChild(link);
              }
            }
            function formatText(command, value) { page.focus(); document.execCommand(command, false, value || null); }
            function pasteText(text) { page.focus(); document.execCommand('insertText', false, text); }
            function setBlock(tag) { page.focus(); document.execCommand('formatBlock', false, tag); }
            function insertLink() {
              const value = prompt(__LINK_PROMPT__);
              if (!value) return;
              try {
                const url = new URL(value);
                if (!['https:', 'http:', 'mailto:'].includes(url.protocol)) return;
                formatText('createLink', url.href);
              } catch (_) { }
            }
            function insertImage(src) { page.focus(); formatText('insertImage', src); }
            function findText(term) { page.focus(); window.find(term, false, false, true, false, false, false); }
            function replaceText(oldText, newText) {
              const walker = document.createTreeWalker(page, NodeFilter.SHOW_TEXT);
              let node;
              while ((node = walker.nextNode())) {
                if ((node.nodeValue || '').includes(oldText)) node.nodeValue = node.nodeValue.replaceAll(oldText, newText || '');
              }
              page.dispatchEvent(new Event('input', { bubbles: true }));
            }
            page.addEventListener('input', () => {
              clearTimeout(inputTimer);
              inputTimer = setTimeout(flushContent, 120);
            });
            page.addEventListener('paste', event => {
              event.preventDefault();
              const text = event.clipboardData?.getData('text/plain') || '';
              const holder = document.createElement('div');
              holder.textContent = text;
              formatText('insertHTML', holder.innerHTML.replace(/\r\n|\n|\r/g, '<br />'));
            });
            page.addEventListener('keydown', event => {
              if (event.ctrlKey && event.key.toLowerCase() === 's') {
                event.preventDefault();
                flushContent();
                chrome.webview.postMessage(JSON.stringify({ type: 'save' }));
              }
            });
          </script>
        </body>
        </html>
        """;
}